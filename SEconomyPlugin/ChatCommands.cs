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
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Formats.Asn1;
using System.Linq;
using System.Threading;
using Terraria;
using TShockAPI;
using Wolfje.Plugins.SEconomy.Journal;
using Wolfje.Plugins.SEconomy.Journal.SqliteJournal;

namespace Wolfje.Plugins.SEconomy
{
	internal class ChatCommands : IDisposable {
		SEconomy Parent { get; set; }

		internal ChatCommands(SEconomy parent)
		{
			this.Parent = parent;
			TShockAPI.Commands.ChatCommands.Add(new TShockAPI.Command(Chat_BankCommand, "bank", "bk") { AllowServer = true });
		}

		protected async void Chat_BankCommand(TShockAPI.CommandArgs args)
		{
			IBankAccount selectedAccount = Parent.GetBankAccount(args.Player);
			IBankAccount callerAccount = Parent.GetBankAccount(args.Player);

			var player = args.Player;

			var GetString = SEconomyPlugin.Locale.StringOrDefault;
			string namePrefix = "Your";

			if (args.Parameters.Count == 0)
			{

				player.SendInfoMessage(GetString(28, "This server is running {0} by Wolfje"), Parent.PluginInstance.GetVersionString());

				player.SendInfoMessage(GetString(230, "You can:"));

				player.SendInfoMessage(GetString(31, $"* View your balance with {TShock.Config.Settings.CommandSpecifier}bank bal"));

				if (player.Group.HasPermission("bank.transfer"))
					player.SendInfoMessage(GetString(32, $"* Trade players with {TShock.Config.Settings.CommandSpecifier}bank pay <player> <amount>"));


				if (player.Group.HasPermission("bank.viewothers"))
					player.SendInfoMessage(GetString(33, $"* View other people's balance with {TShock.Config.Settings.CommandSpecifier}bank bal <player>"));

				if (player.Group.HasPermission("bank.worldtransfer"))
					player.SendInfoMessage(GetString(34, $"* Spawn/delete money with {TShock.Config.Settings.CommandSpecifier}bank give|take <player> <amount>"));

				if (player.Group.HasPermission("bank.savejournal"))
					player.SendInfoMessage(GetString(36, $"* Save the journal with {TShock.Config.Settings.CommandSpecifier}bank savejournal"));

				if (player.Group.HasPermission("bank.loadjournal"))
					player.SendInfoMessage(GetString(37, $"* Load the journal with {TShock.Config.Settings.CommandSpecifier}bank loadjournal"));

				if (player.Group.HasPermission("bank.squashjournal"))
					player.SendInfoMessage(GetString(38, $"* Compress the journal with {TShock.Config.Settings.CommandSpecifier}bank squashjournal"));

				if (player.Group.HasPermission("bank.reset"))
					player.SendInfoMessage($"* Reset players' bank account with {TShock.Config.Settings.CommandSpecifier}bank reset <player/*>");

				if (player.Group.HasPermission("bank.leaderboard"))
					player.SendInfoMessage($"* Show the leaderboard for everyone's balance with {TShock.Config.Settings.CommandSpecifier}bank lb <Page>");

				if (player.Group.HasPermission("bank.xmlsqlite"))
					player.SendInfoMessage($"* Generate an Sqlite journal using and already existing Xml journal with {TShock.Config.Settings.CommandSpecifier}bank xml2sqlite");

				return;
			}


			switch (args.Parameters[0])
			{
				case "reset":

					if (!player.HasPermission("bank.reset"))
					{
						player.SendErrorMessage(GetString(42, "[SEconomy Reset] You do not have permission to perform this command."));
						return;
					}

					if (args.Parameters.Count == 1 || string.IsNullOrEmpty(args.Parameters[1]))
					{
						player.SendErrorMessage("[SEconomy Reset] Invalid subcommand usage.");
						return;
					}


					if (args.Parameters[1] == "*")
					{

						player.SendInfoMessage(string.Format("[SEconomy Reset] Resetting {0} bank accounts."), Parent.RunningJournal.BankAccounts.Count);

						foreach (var acc in Parent.RunningJournal.BankAccounts)
						{
							acc.Transactions.Clear();

							await acc.SyncBalanceAsync();

						}

						player.SendInfoMessage(GetString(40, "[SEconomy Reset] Reset complete."));

						return;
					}


					IBankAccount targetAccount = Parent.RunningJournal.GetBankAccountByName(args.Parameters[1]);

					if (targetAccount == null)
					{
						player.SendErrorMessage(string.Format(GetString(41, "[SEconomy Reset] Cannot find player \"{0}\" or no bank account found."), args.Parameters[1]));
						return;
					}

					player.SendInfoMessage(string.Format(GetString(39, "[SEconomy Reset] Resetting {0}'s account."), args.Parameters[1]));

					targetAccount.Transactions.Clear();

					await targetAccount.SyncBalanceAsync();

					player.SendInfoMessage(GetString(40, "[SEconomy Reset] Reset complete."));

					return;

				case "bal":
				case "balance":

					if (player.HasPermission("bank.viewothers") && args.Parameters.Count >= 2)
						selectedAccount = Parent.RunningJournal.GetBankAccountByName(args.Parameters[1]);

					if (selectedAccount == null)
					{
						player.SendInfoMessage(GetString(46, "[Bank Balance] Cannot find player or bank account (You might need to login)."));
						return;
					}

					if (selectedAccount != callerAccount)
						player.SendInfoMessage(String.Format(" [Balance] Showing {0}'s bank account:", selectedAccount.UserAccountName));

					player.SendInfoMessage(GetString(44, "[Balance] {0} {1}"), selectedAccount.Balance.ToString(),
					selectedAccount.IsAccountEnabled ? "" : GetString(45, "[c/ff0000:(This Account is disabled)]"));

					return;
				case "sj":
				case "savejournal":
					if (!player.HasPermission("bank.savejournal"))
					{
						player.SendErrorMessage(GetString(42, "[SEconomy SaveJournal] You do not have permission to perform this command."));
						return;
					}

					player.SendInfoMessage(GetString(51, "[SEconomy XML] Backing up transaction journal."));

					await Parent.RunningJournal.SaveJournalAsync();

					player.SendInfoMessage("[SEconomy XML] Finished backing up, Please refer to the console for process details.");

					return;

				case "lj":
				case "loadjournal":
					if (!player.HasPermission("bank.loadjournal"))
					{
						player.SendErrorMessage(GetString(42, "[SEconomy LoadJournal] You do not have permission to perform this command."));
						return;
					}

					if (SEconomyPlugin.Instance.RunningJournal.JournalSaving)
					{
						player.SendErrorMessage("[SEconomy LoadJournal] Error: Journal is currently saving.");
						return;
					}

					player.SendInfoMessage(GetString(52, "[SEconomy XML] Loading transaction journal from file"));

					bool oldValue = SEconomyPlugin.Instance.RunningJournal.BackupsEnabled;

					SEconomyPlugin.Instance.RunningJournal.BackupsEnabled = false;

					SEconomyPlugin.Instance.RunningJournal.BankAccounts.Clear();

					await Parent.RunningJournal.LoadJournalAsync();

					SEconomyPlugin.Instance.RunningJournal.BackupsEnabled = oldValue;

					player.SendInfoMessage("[SEconomy XML] Finished loading transaction journal, Please refer to the console for process details.");

					return;

				case "sqj":
				case "squashjournal":
					if (!player.HasPermission("bank.squashjournal"))
					{
						player.SendErrorMessage(GetString(42, "[SEconomy Journal] You do not have permission to perform this command."));
						return;
					}

					player.SendInfoMessage("[SEconomy Journal] Beginning Squash");

					await Parent.RunningJournal.SquashJournalAsync();

					player.SendInfoMessage("[SEconomy Journal] Finished Squash");

					return;

				case "pay":
				case "transfer":
				case "tfr":
					if (!player.HasPermission("bank.transfer"))
					{
						player.SendErrorMessage(GetString(42, "[SEconomy Pay] You do not have permission to perform this command."));
						return;
					}

					// /bank pay wolfje 1p
					if (args.Parameters.Count < 3)
					{
						player.SendErrorMessage(GetString(56, $"Usage: {TShock.Config.Settings.CommandSpecifier}bank pay [Player] [Amount]"));
						return;
					}

					selectedAccount = Parent.GetPlayerBankAccount(args.Parameters[1]);

					if (selectedAccount == null)
					{
						player.SendErrorMessage(GetString(54, "Cannot find player by the name of \"{0}\""), args.Parameters[1]);
						return;
					}

					if (!Money.TryParse(args.Parameters[2], out var amount))
					{
						player.SendErrorMessage("[Bank Pay] \"{0}\" isn't a valid amount of money.", args.Parameters[2]);
						return;
					}
					
					if (callerAccount == null)
					{
						player.SendErrorMessage("[Bank Pay] Bank account error.");
						return;
					}

                    if (callerAccount.Balance < amount && !callerAccount.IsSystemAccount)
                    {
                        player.SendErrorMessage("[Bank Pay] Theres less than {0} stored in your bank account", amount.ToString());
                        return;
                    }


                    //Instruct the world bank to give the player money.
                    await callerAccount.TransferToAsync(selectedAccount, amount,
							BankAccountTransferOptions.AnnounceToReceiver
							| BankAccountTransferOptions.AnnounceToSender
							| BankAccountTransferOptions.IsPlayerToPlayerTransfer,
						string.Format("{0} >> {1}", player.Name, selectedAccount.UserAccountName),
						string.Format("SE: tfr: {0} to {1} for {2}", player.Name, selectedAccount.UserAccountName, amount.ToString()));

					player.SendSuccessMessage("[Bank Pay] Successfully gave {0} to {1}.", amount.ToString(), selectedAccount.UserAccountName);

					var playerSelected = TSPlayer.FindByNameOrID(selectedAccount.UserAccountName).FirstOrDefault();

					if (playerSelected != null)
						player.SendInfoMessage("[Bank Pay] {0} gave you {1}.", args.Player.Name, amount.ToString());

					return;

				case "give":
				case "take":
					if (!player.HasPermission("bank.worldtransfer"))
					{
						player.SendErrorMessage(GetString(42, "[SEconomy Reset] You do not have permission to perform this command."));
						return;
					}

					// /bank give wolfje 1p
					if (args.Parameters.Count < 3)
					{
						player.SendErrorMessage(GetString(58, $"Usage: {TShock.Config.Settings.CommandSpecifier}bank give|take <Player Name> <Amount>"));
						return;
					}

					selectedAccount = Parent.GetPlayerBankAccount(args.Parameters[1]);

					if (selectedAccount == null)
					{
						player.SendErrorMessage("Cannot find player by the name of {0}.", args.Parameters[1]);
						return;
					}

					if (!Money.TryParse(args.Parameters[2], out amount))
					{
						player.SendErrorMessage(GetString(57, "[Bank Give] You don't have permission to do that."));
						return;
					}

					//eliminate a double-negative.  saying "take Player -1p1c" will give them 1 plat 1 copper!
					if (args.Parameters[0].Equals("take", StringComparison.CurrentCultureIgnoreCase) && amount > 0)
						amount = -amount;

					//Instruct the world bank to give the player money.
					Parent.WorldAccount.TransferTo(selectedAccount, amount, Journal.BankAccountTransferOptions.AnnounceToReceiver,
												   args.Parameters[0] + " command", string.Format("SE: pay: {0} to {1} ",
												   amount.ToString(), args.Parameters[1]));

					playerSelected = TSPlayer.FindByNameOrID(selectedAccount.UserAccountName).FirstOrDefault();

					if (args.Parameters[0].Equals("take", StringComparison.CurrentCultureIgnoreCase))
					{
						playerSelected?.SendInfoMessage("[Bank Take] {0} took {1} from you.", args.Player.Name, amount.ToString());
						player.SendInfoMessage("[Bank Take] successfully took {0} from {1}.", amount.ToString(), player.Name);
					}
					else
					{
						playerSelected?.SendInfoMessage("[Bank Give] {0} gave you {1} using world account.", args.Player.Name, amount.ToString());
						player.SendInfoMessage("[Bank Give] successfully gave {0} {1} using world account.", player.Name, amount.ToString());
					}

					return;

				case "leaderboard":
				case "lb":
					if (!player.HasPermission("bank.leaderboard"))
					{
						player.SendErrorMessage(GetString(42, "[SEconomy Leaderboard] You do not have permission to perform this command."));
						return;
					}

					if(Parent.RunningJournal.BankAccounts.Count <= 1)
					{
						player.SendMessage("[SEconomy Leaderboard] There is currently no one on the leaderboard.", Color.Red);
						return;
					}

					int page = 1;

					if (args.Parameters.Count == 2 && !PaginationTools.TryParsePageNumber(args.Parameters, 1, player, out page))
					{
						args.Player.SendErrorMessage($"Usage: {TShock.Config.Settings.CommandSpecifier}bank lb <Page>");
						return;
					}


					List<string> rankStrings = new();

					(string, int, string)[] ranks = SEconomyPlugin.Instance.Configuration.LeaderBoardRanks;

					int i = 1;
					int j = -1;

					List<IBankAccount> sortedAccounts = Parent.RunningJournal.BankAccounts.OrderBy(i => -i.Balance).ToList();

					int sequence = 0;

					int fibonacciLastNum = 0;

					int fibonacciCurrentNum = 1;

					int maxPage = (int)Math.Ceiling((decimal)sortedAccounts.Count / SEconomyPlugin.Instance.Configuration.MaxRanksPerPage);

					if (page > maxPage)
						page = maxPage;

					foreach (var acc in sortedAccounts)
					{
						if (acc.IsSystemAccount)
							continue;

						if ((int)Math.Ceiling((decimal)i / SEconomyPlugin.Instance.Configuration.MaxRanksPerPage) > page)
							break;

						if (sequence == 0 && j < ranks.Length - 1)
						{
							if (SEconomyPlugin.Instance.Configuration.LeaderboardAvailablePositionsStyle.Equals("positionperrank", StringComparison.CurrentCultureIgnoreCase)
							   || SEconomyPlugin.Instance.Configuration.LeaderboardAvailablePositionsStyle.Equals("posperrank", StringComparison.CurrentCultureIgnoreCase))
								sequence += SEconomyPlugin.Instance.Configuration.LeaderboardPositionsPerRanks;

							else
							{
								int lastNum = fibonacciCurrentNum;

								sequence = fibonacciCurrentNum = fibonacciLastNum + fibonacciCurrentNum;

								fibonacciLastNum = lastNum;

							}

							j++;
						}
						

						if(page == (int)Math.Ceiling((decimal)i / SEconomyPlugin.Instance.Configuration.MaxRanksPerPage))
							rankStrings.Add(String.Format("[c/#FFFAE4:{1}] [c/{0}:| {2}] [c/{0}:-] [i:{3}] [c/{0}:{4}] [i:{3}] [c/{0}::] {5}", ranks[j].Item3, i,
									    	ranks[j].Item1, ranks[j].Item2, acc.UserAccountName, acc.Balance.ToString()));


						i++;
						sequence--;
					}


					player.SendMessage("SEconomy Leaderboard ({0}/{1}):".SFormat(page, maxPage), Color.Green);

					foreach (string str in rankStrings)
						player.SendInfoMessage(str);

					if (page != maxPage)
						player.SendMessage("Type {0}bank lb {1} for more.".SFormat(Commands.Specifier, page + 1), Color.Yellow);
					else
						player.SendMessage("You are on the last page.", Color.Yellow);

					return;

				case "xmltosqlite":
				case "xml2sqlite":
					
					if(!player.HasPermission("bank.xmlsqlite"))
					{
                        player.SendErrorMessage(GetString(42, "[SEconomy Xml2Sqlite] You do not have permission to perform this command."));
                        return;
                    }

                    if (SEconomyPlugin.Instance.Configuration.JournalType != "xml")
					{
						player.SendErrorMessage("[SEconomy Xml2Sqlite] you arent using an XML journal");
						return;
					}

					try
					{

						TShock.Log.ConsoleInfo("[SEconomy Journal] Creating SQLite DB from XML journal");

						TShock.Log.ConsoleInfo("[SEconomy Jounral] Squashing journal before convertion");

						await SEconomyPlugin.Instance.RunningJournal.SquashJournalAsync();

						SqliteTransactionJournal journal = new SqliteTransactionJournal(SEconomyPlugin.Instance, Config.SqliteDbPath);

						await journal.LoadJournalAsync();

						var worldAcc = new SqliteBankAccount(journal)
						{
							UserAccountName = " ",
							WorldID = Main.worldID,
							Flags = Journal.BankAccountFlags.Enabled
						| Journal.BankAccountFlags.LockedToWorld
						| Journal.BankAccountFlags.SystemAccount,
							Description = "World account for world " + Main.worldName
						};

						journal.AddBankAccount(worldAcc);

						foreach (IBankAccount acc in SEconomyPlugin.Instance.RunningJournal.BankAccounts)
						{
							if (acc == null || acc.IsSystemAccount)
								continue;

							journal.AddBankAccount(acc.UserAccountName, acc.WorldID, acc.Flags, "Xml -> Sqlite");

							worldAcc.TransferTo(journal.GetBankAccountByName(acc.UserAccountName), (acc.Transactions.FirstOrDefault() == null) ? 0 : acc.Transactions.FirstOrDefault().Amount,
												BankAccountTransferOptions.None, "Transfered from Xml account", "Transfered from Xml account");

						}

						TShock.Log.ConsoleInfo("[SEconomy Journal] Created a new SQLite journal from the XML one, to use your new journal please reload the plugin with the journalType config set to 'sqlite'");
					}
					catch (Exception e)
					{
						TShock.Log.ConsoleError("[SEconomy Journal] failed to create an SQLite journal from the XML one: " + "\n" + e.Message + "\n " + e.StackTrace);
					}
                    return;

				default:
                    player.SendErrorMessage("[Bank] Invalid subcommand.");
					return;
            }



		}


		public void Dispose()
		{
			this.Dispose(true);
			GC.SuppressFinalize(this);
		}

		protected virtual void Dispose(bool disposing)
		{
			if (disposing == true) {
				TShockAPI.Command bankCommand = TShockAPI.Commands.ChatCommands.FirstOrDefault(i => i.Name == "bank" && i.CommandDelegate == Chat_BankCommand);
				if (bankCommand != null) {
					TShockAPI.Commands.ChatCommands.Remove(bankCommand);
				}
			}
		}
	}
}
