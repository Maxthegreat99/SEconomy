/*
 * This file is part of SEconomy - A server-sided currency implementation
 * Copyright (C) 2013-2014, Tyler Watson <tyler@tw.id.au>
 *
 * This program is free software; you can redistribute it and/or
 * modify it under the terms of the GNU General Public License
 * as published by the Free Software Foundation; either version 2
 * of the License, or (at your option) any later version.
 * 
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 * 
 * You should have received a copy of the GNU General Public License
 * along with this program; if not, write to the Free Software
 * Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.
 */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Terraria;
using TShockAPI;
using Wolfje.Plugins.SEconomy.Journal;

namespace Wolfje.Plugins.SEconomy
{
	public class SEconomy : IDisposable {
		public Journal.ITransactionJournal RunningJournal { get; set; }

		public Config Configuration { get; set; }

		public SEconomyPlugin PluginInstance { get; set; }

		public Journal.JournalTransactionCache TransactionCache { get; set; }

		public Journal.IBankAccount WorldAccount { get; internal set; }

		public WorldEconomy WorldEc { get; internal set; }

		internal EventHandlers EventHandlers { get; set; }

		internal ChatCommands ChatCommands { get; set; }

		public Dictionary<Player, DateTime> IdleCache { get; protected set; }

		public SEconomy(SEconomyPlugin PluginInstance)
		{
			this.IdleCache = new Dictionary<Player, DateTime>();
			this.PluginInstance = PluginInstance;
		}

		public string cmdStarter = TShock.Config.Settings.CommandSpecifier;
		
		#region "Loading and setup"

		public bool IsNet45OrNewer()
		{
			// Class "ReflectionContext" exists from .NET 4.5 onwards.
			return Type.GetType("System.Reflection.ReflectionContext", false) != null;
		}

		public int LoadSEconomy()
		{
			if (IsNet45OrNewer() == false) {
				TShock.Log.ConsoleError("SEconomy requires Microsoft .NET framework 4.5 or later.");
				TShock.Log.ConsoleError("SEconomy will not run.");
				return -1;
			}

			try {
				this.Configuration = Config.FromFile(Config.BaseDirectory + System.IO.Path.DirectorySeparatorChar + "SEconomy.config.json");
				if (LoadJournal() == false) {
					return -1;
				}
				this.WorldEc = new WorldEconomy(this);
				this.EventHandlers = new EventHandlers(this);
				this.ChatCommands = new ChatCommands(this);
				this.TransactionCache = new Journal.JournalTransactionCache();
			} catch (Exception) {
				return -1;
			}

			return 0;
		}

		protected bool LoadJournal()
		{
			Journal.ITransactionJournal journal = null;
			if (Configuration == null) {
				return false;
			}

			if (Configuration.JournalType.Equals("xml", StringComparison.InvariantCultureIgnoreCase) == true) {
				Wolfje.Plugins.SEconomy.Journal.XMLJournal.XmlTransactionJournal xmlJournal = new Journal.XMLJournal.XmlTransactionJournal(this, Config.JournalPath);
				xmlJournal.JournalLoadingPercentChanged += (sender, args) => ConsoleEx.WriteBar(args);

				journal = xmlJournal;
			} else if (Configuration.JournalType.Equals("mysql", StringComparison.InvariantCultureIgnoreCase) == true
			           || Configuration.JournalType.Equals("sql", StringComparison.InvariantCultureIgnoreCase) == true) {
				Wolfje.Plugins.SEconomy.Journal.MySQLJournal.MySQLTransactionJournal sqlJournal = new Journal.MySQLJournal.MySQLTransactionJournal(this, Configuration.SQLConnectionProperties);
				sqlJournal.JournalLoadingPercentChanged += (sender, args) => ConsoleEx.WriteBar(args);

				journal = sqlJournal;
			}

			this.RunningJournal = journal;
			if (this.RunningJournal.LoadJournal() == false) {
				return false;
			}

			return true;
		}

		internal async Task<Journal.IBankAccount> CreatePlayerAccountAsync(TSPlayer player)
		{
			Money startingMoney;
			Journal.IBankAccount newAccount = SEconomyPlugin.Instance.RunningJournal.AddBankAccount(player.Account.Name, 
				                                  Main.worldID, 
				                                  Journal.BankAccountFlags.Enabled, 
				                                  "");

			TShock.Log.ConsoleInfo(string.Format("[SEconomy] Bank account for {0} logged in as {1} created.", player.Name, player.Account.Name));

			if (Money.TryParse(SEconomyPlugin.Instance.Configuration.StartingMoney, out startingMoney)
			    && startingMoney > 0) {

				await SEconomyPlugin.Instance.WorldAccount.TransferToAsync(newAccount, startingMoney, 
					Journal.BankAccountTransferOptions.AnnounceToReceiver, 
					"starting out.", "starting out.");
			}

			return newAccount;
		}

		/// <summary>
		/// Called after LoadSEconomy, or on PostInitialize, this binds the current SEconomy instance
		/// to the currently running terraria world.
		/// </summary>
		public async Task BindToWorldAsync()
		{
			IBankAccount account = null;

			if ((WorldAccount = RunningJournal.GetWorldAccount()) == null) {
				TShock.Log.ConsoleError("[SEconomy Bind] The journal system did not return a world account. This is an internal error.");
				return;
			}

			await WorldAccount.SyncBalanceAsync();
			TShock.Log.ConsoleInfo(string.Format(SEconomyPlugin.Locale.StringOrDefault(1, "[SEconomy World Account] Paid {0} to players."), WorldAccount.Balance.ToLongString()));

			await Task.Delay(5000);
			foreach (var player in TShockAPI.TShock.Players) {
				if (player == null
				    || string.IsNullOrWhiteSpace(player.Name) == true
				    //|| string.IsNullOrWhiteSpace(player.User.Name) == true
				    || (account = GetBankAccount(player)) == null) {
					continue;
				}
                
				await account.SyncBalanceAsync();
			}
		}

		#endregion

		#region "Player idle caching"

		public void RemovePlayerIdleCache(Player player)
		{
			if (IdleCache == null
			    || player == null
			    || IdleCache.ContainsKey(player) == false) {
				return;
			}

			IdleCache.Remove(player);
		}

		public TimeSpan? PlayerIdleSince(Player player)
		{
			if (player == null
			    || IdleCache == null
			    || IdleCache.ContainsKey(player) == false) {
				return null;
			}

			return DateTime.UtcNow.Subtract(IdleCache[player]);
		}

		public void UpdatePlayerIdle(Player player)
		{
			if (player == null || IdleCache == null) {
				return;
			}

			if (IdleCache.ContainsKey(player) == false) {
				IdleCache.Add(player, DateTime.UtcNow);
			}

			IdleCache[player] = DateTime.UtcNow;
		}

		#endregion

		public int PurgeAccounts()
		{
			
			return 0;
		}

		public IBankAccount GetBankAccount(TSPlayer tsPlayer)
		{
			if (tsPlayer == null || RunningJournal == null || !tsPlayer.IsLoggedIn) {
				return null;
			}

			if (tsPlayer == TSPlayer.Server) {
				return WorldAccount;
			}

			try {
				return RunningJournal.GetBankAccountByName(tsPlayer.Account.Name);
			} catch (Exception ex) {
				TShock.Log.ConsoleError("[SEconomy Error] Error getting bank account for {0}: {1}", 
					tsPlayer.Account.Name, ex.Message);
				return null;
			}
		}

		public IBankAccount GetBankAccount(Player player)
		{
			return GetBankAccount(player.whoAmI);
		}

		public IBankAccount GetBankAccount(string userAccountName)  // this overload seems not used from anywhere
		{
			return GetBankAccount(TShockAPI.TShock.Players.FirstOrDefault(i => i != null && i.Account.Name == userAccountName));
		}

		public IBankAccount GetPlayerBankAccount(string playerName)
		{
			return GetBankAccount(TShockAPI.TShock.Players.FirstOrDefault(i => i != null && i.Name == playerName));
		}

		public Task<List<KeyValuePair<TSPlayer, IBankAccount>>> SearchPlayerBankAccountAsync(string playerName)
		{
			return Task.Run(() => SearchPlayerBankAccount(playerName));
		}

		public List<KeyValuePair<TSPlayer, IBankAccount>> SearchPlayerBankAccount(string playerName)
		{
			List<KeyValuePair<TSPlayer, IBankAccount>> accountList = new List<KeyValuePair<TSPlayer, IBankAccount>>();
			IBankAccount targetAccount;

			foreach (TSPlayer player in TShockAPI.TShock.Players) {
				if (player == null
				    || (targetAccount = GetBankAccount(player)) == null) {
					continue;
				}

				accountList.Add(new KeyValuePair<TSPlayer, IBankAccount>(player, targetAccount));
			}

			return accountList;
		}


		public IBankAccount GetBankAccount(int who)
		{
			return who < 0 ? GetBankAccount(TSPlayer.Server) : GetBankAccount(TShockAPI.TShock.Players.FirstOrDefault(i => i != null && i.Index == who));
		}

		/// <summary>
		/// Gets an economy-enabled player by their player name. 
		/// </summary>
		[Obsolete("Use GetBankAccount() instead.", true)]
		public object GetEconomyPlayerByBankAccountNameSafe(string Name)
		{
			throw new NotSupportedException("Use GetBankAccount() instead.");
		}

		/// <summary>
		/// Gets an economy-enabled player by their index.  This method is thread-safe.
		/// </summary>
		[Obsolete("Use GetBankAccount() instead.", true)]
		public object GetEconomyPlayerSafe(int Id)
		{
			throw new NotSupportedException("Use GetBankAccount() instead.");
		}

		/// <summary>
		/// Gets an economy-enabled player by their player name. 
		/// </summary>
		[Obsolete("Use GetBankAccount() instead.", true)]
		public object GetEconomyPlayerSafe(string Name)
		{
			throw new NotSupportedException("Use GetBankAccount() instead.");
		}

		#region "IDisposable"

		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		protected virtual void Dispose(bool disposing)
		{
			if (disposing == true) {
				if (this.EventHandlers != null) {
					this.EventHandlers.Dispose();
				}
				if (this.ChatCommands != null) {
					this.ChatCommands.Dispose();
				}
				if (this.WorldEc != null) {
					this.WorldEc.Dispose();
				}
				if (this.TransactionCache != null) {
					this.TransactionCache.Dispose();
				}
				if (this.RunningJournal != null) {
					this.RunningJournal.Dispose();
				}
				this.Configuration = null;
			}

		}

		#endregion
	}
}
