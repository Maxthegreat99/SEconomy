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
using System.Linq;
using System.Threading;
using TShockAPI;
using Wolfje.Plugins.SEconomy.Journal;

namespace Wolfje.Plugins.SEconomy
{
	internal class ChatCommands : IDisposable {
		SEconomy Parent { get; set; }

		internal ChatCommands(SEconomy parent)
		{
			this.Parent = parent;
			TShockAPI.Commands.ChatCommands.Add(new TShockAPI.Command(Chat_BankCommand, "bank") { AllowServer = true });
		}

		protected async void Chat_BankCommand(TShockAPI.CommandArgs args)
		{
			IBankAccount selectedAccount = Parent.GetBankAccount(args.Player);
			IBankAccount callerAccount = Parent.GetBankAccount(args.Player);
			string namePrefix = "Your";

			if (args.Parameters.Count == 0) {
				args.Player.SendInfoMessage(SEconomyPlugin.Locale.StringOrDefault(28, "This server is running {0} by Wolfje"), Parent.PluginInstance.GetVersionString());
				//args.Player.SendInfoMessage(SEconomyPlugin.Locale.StringOrDefault(29, "Download here: http://plugins.tw.id.au")); //Site is dead, and this is not maintained by wolfje anymore
				args.Player.SendInfoMessage(SEconomyPlugin.Locale.StringOrDefault(230, "You can:"));

				args.Player.SendInfoMessage(SEconomyPlugin.Locale.StringOrDefault(31, $"* View your balance with {TShock.Config.Settings.CommandSpecifier}bank bal"));

				if (args.Player.Group.HasPermission("bank.transfer")) {
					args.Player.SendInfoMessage(SEconomyPlugin.Locale.StringOrDefault(32, $"* Trade players with {TShock.Config.Settings.CommandSpecifier}bank pay <player> <amount>"));
				}

				if (args.Player.Group.HasPermission("bank.viewothers")) {
					args.Player.SendInfoMessage(SEconomyPlugin.Locale.StringOrDefault(33, $"* View other people's balance with {TShock.Config.Settings.CommandSpecifier}bank bal <player>"));
				}

				if (args.Player.Group.HasPermission("bank.worldtransfer")) {
					args.Player.SendInfoMessage(SEconomyPlugin.Locale.StringOrDefault(34, $"* Spawn/delete money with {TShock.Config.Settings.CommandSpecifier}bank give|take <player> <amount>"));
				}

				if (args.Player.Group.HasPermission("bank.mgr")) {
					args.Player.SendInfoMessage(SEconomyPlugin.Locale.StringOrDefault(35, $"* Spawn the account manager GUI on the server with {TShock.Config.Settings.CommandSpecifier}bank mgr"));
				}

				if (args.Player.Group.HasPermission("bank.savejournal")) {
					args.Player.SendInfoMessage(SEconomyPlugin.Locale.StringOrDefault(36, $"* Save the journal with {TShock.Config.Settings.CommandSpecifier}bank savejournal"));
				}

				if (args.Player.Group.HasPermission("bank.loadjournal")) {
					args.Player.SendInfoMessage(SEconomyPlugin.Locale.StringOrDefault(37, $"* Load the journal with {TShock.Config.Settings.CommandSpecifier}bank loadjournal"));
				}

				if (args.Player.Group.HasPermission("bank.squashjournal")) {
					args.Player.SendInfoMessage(SEconomyPlugin.Locale.StringOrDefault(38, $"* Compress the journal with {TShock.Config.Settings.CommandSpecifier}bank squashjournal"));
				}

				return;
			}

			if (args.Parameters[0].Equals("reset", StringComparison.CurrentCultureIgnoreCase)) {
				if (args.Player.Group.HasPermission("seconomy.reset")) {
					if (args.Parameters.Count >= 2 && !string.IsNullOrEmpty(args.Parameters[1])) {
						IBankAccount targetAccount = Parent.GetPlayerBankAccount(args.Parameters[1]);

						if (targetAccount != null) {
							args.Player.SendInfoMessage(string.Format(SEconomyPlugin.Locale.StringOrDefault(39, "[SEconomy Reset] Resetting {0}'s account."), args.Parameters[1]));
							targetAccount.Transactions.Clear();
							await targetAccount.SyncBalanceAsync();
							args.Player.SendInfoMessage(SEconomyPlugin.Locale.StringOrDefault(40, "[SEconomy Reset] Reset complete."));
						} else {
							args.Player.SendErrorMessage(string.Format(SEconomyPlugin.Locale.StringOrDefault(41, "[SEconomy Reset] Cannot find player \"{0}\" or no bank account found."), args.Parameters[1]));
						}
					}
				} else {
					args.Player.SendErrorMessage(SEconomyPlugin.Locale.StringOrDefault(42, "[SEconomy Reset] You do not have permission to perform this command."));
				}
			}

			//Bank balance
			if (args.Parameters[0].Equals("bal", StringComparison.CurrentCultureIgnoreCase)
			    || args.Parameters[0].Equals("balance", StringComparison.CurrentCultureIgnoreCase)) {


				//The command supports viewing other people's balance if the caller has permission
				if (args.Player.Group.HasPermission("bank.viewothers")) {
					if (args.Parameters.Count >= 2) {
						selectedAccount = Parent.GetPlayerBankAccount(args.Parameters[1]);
						namePrefix = args.Parameters[1] + "'s";
					}
				}

				if (selectedAccount != null) {
					if (!selectedAccount.IsAccountEnabled && !args.Player.Group.HasPermission("bank.viewothers")) {
						args.Player.SendErrorMessage(SEconomyPlugin.Locale.StringOrDefault(43, "[Bank Balance] Your account is disabled."));
					}
					else
					{
						args.Player.SendInfoMessage(SEconomyPlugin.Locale.StringOrDefault(44, "[Balance] {0} {1}"), selectedAccount.Balance.ToString(),
						selectedAccount.IsAccountEnabled ? "" : SEconomyPlugin.Locale.StringOrDefault(45, "[c/ff0000:(This Account is disabled)]"));
					}
				} else {
					args.Player.SendInfoMessage(SEconomyPlugin.Locale.StringOrDefault(46, "[Bank Balance] Cannot find player or bank account (You might need to login)."));
				}
			} else if (args.Parameters[0].Equals("mgr")) {

			} else if (args.Parameters[0].Equals("savejournal")) {
				if (args.Player.Group.HasPermission("bank.savejournal")) {
					args.Player.SendInfoMessage(SEconomyPlugin.Locale.StringOrDefault(51, "[SEconomy XML] Backing up transaction journal."));

					await Parent.RunningJournal.SaveJournalAsync();
				}

			} else if (args.Parameters[0].Equals("loadjournal")) {
				if (args.Player.Group.HasPermission("bank.loadjournal")) {
					args.Player.SendInfoMessage(SEconomyPlugin.Locale.StringOrDefault(52, "[SEconomy XML] Loading transaction journal from file"));

					await Parent.RunningJournal.LoadJournalAsync();
				}

			} else if (args.Parameters[0].Equals("squashjournal", StringComparison.CurrentCultureIgnoreCase)) {
				if (args.Player.Group.HasPermission("bank.squashjournal")) {
					await Parent.RunningJournal.SquashJournalAsync();
					await Parent.RunningJournal.SaveJournalAsync();
				} else {
					args.Player.SendErrorMessage(SEconomyPlugin.Locale.StringOrDefault(53, "[Bank SquashJournal] You do not have permission to perform this command."));
				}
			} else if (args.Parameters[0].Equals("pay", StringComparison.CurrentCultureIgnoreCase)
			           || args.Parameters[0].Equals("transfer", StringComparison.CurrentCultureIgnoreCase)
			           || args.Parameters[0].Equals("tfr", StringComparison.CurrentCultureIgnoreCase)) {
				//Player-to-player transfer

				if (args.Player.Group.HasPermission("bank.transfer")) {
					// /bank pay wolfje 1p
					if (args.Parameters.Count >= 3) {
						selectedAccount = Parent.GetPlayerBankAccount(args.Parameters[1]);
						Money amount = 0;

						if (selectedAccount == null) {
							args.Player.SendErrorMessage(SEconomyPlugin.Locale.StringOrDefault(54, "Cannot find player by the name of \"{0}\""), args.Parameters[1]);
						} else {
							if (Money.TryParse(args.Parameters[2], out amount)) {
								if (callerAccount == null) {
									args.Player.SendErrorMessage("[Bank Pay] Bank account error.");
									return;
								}
								//Instruct the world bank to give the player money.
								await callerAccount.TransferToAsync(selectedAccount, amount, 
									Journal.BankAccountTransferOptions.AnnounceToReceiver 
										| Journal.BankAccountTransferOptions.AnnounceToSender 
										| Journal.BankAccountTransferOptions.IsPlayerToPlayerTransfer, 
									string.Format("{0} >> {1}", args.Player.Name, args.Parameters[1]),
									string.Format("SE: tfr: {0} to {1} for {2}", args.Player.Name, args.Parameters[1], amount.ToString()));
							} else {
								args.Player.SendErrorMessage(SEconomyPlugin.Locale.StringOrDefault(55, "[Bank Give] \"{0}\" isn't a valid amount of money."), args.Parameters[2]);
							}
						}
					} else {
						args.Player.SendErrorMessage(SEconomyPlugin.Locale.StringOrDefault(56, $"Usage: {TShock.Config.Settings.CommandSpecifier}bank pay [Player] [Amount]"));
					}
				} else {
					args.Player.SendErrorMessage(SEconomyPlugin.Locale.StringOrDefault(57, "[Bank Pay] You don't have permission to do that."));
				}

			} else if (args.Parameters[0].Equals("give", StringComparison.CurrentCultureIgnoreCase)
			           || args.Parameters[0].Equals("take", StringComparison.CurrentCultureIgnoreCase)) {
				//World-to-player transfer

				if (args.Player.Group.HasPermission("bank.worldtransfer")) {
					// /bank give wolfje 1p
					if (args.Parameters.Count >= 3) {
						selectedAccount = Parent.GetPlayerBankAccount(args.Parameters[1]);
						Money amount = 0;

						if (selectedAccount == null) {
							args.Player.SendErrorMessage(SEconomyPlugin.Locale.StringOrDefault(54, "Cannot find player by the name of {0}."), args.Parameters[1]);
						} else {
							if (Money.TryParse(args.Parameters[2], out amount)) {

								//eliminate a double-negative.  saying "take Player -1p1c" will give them 1 plat 1 copper!
								if (args.Parameters[0].Equals("take", StringComparison.CurrentCultureIgnoreCase) && amount > 0) {
									amount = -amount;
								}

								//Instruct the world bank to give the player money.
								Parent.WorldAccount.TransferTo(selectedAccount, amount, Journal.BankAccountTransferOptions.AnnounceToReceiver, args.Parameters[0] + " command", string.Format("SE: pay: {0} to {1} ", amount.ToString(), args.Parameters[1]));
							} else {
								args.Player.SendErrorMessage(SEconomyPlugin.Locale.StringOrDefault(55, "[Bank Give] \"{0}\" isn't a valid amount of money."), args.Parameters[2]);
							}
						}
					} else {
						args.Player.SendErrorMessage(SEconomyPlugin.Locale.StringOrDefault(58, $"Usage: {TShock.Config.Settings.CommandSpecifier}bank give|take <Player Name> <Amount>"));
					}
				} else {
					args.Player.SendErrorMessage(SEconomyPlugin.Locale.StringOrDefault(57, "[Bank Give] You don't have permission to do that."));
				}
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
