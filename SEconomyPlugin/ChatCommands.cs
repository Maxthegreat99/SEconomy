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
using System.Threading;
using Terraria;
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

				if (args.Player.Group.HasPermission("bank.savejournal")) {
					args.Player.SendInfoMessage(SEconomyPlugin.Locale.StringOrDefault(36, $"* Save the journal with {TShock.Config.Settings.CommandSpecifier}bank savejournal"));
				}

				if (args.Player.Group.HasPermission("bank.loadjournal")) {
					args.Player.SendInfoMessage(SEconomyPlugin.Locale.StringOrDefault(37, $"* Load the journal with {TShock.Config.Settings.CommandSpecifier}bank loadjournal"));
				}

				if (args.Player.Group.HasPermission("bank.squashjournal")) {
					args.Player.SendInfoMessage(SEconomyPlugin.Locale.StringOrDefault(38, $"* Compress the journal with {TShock.Config.Settings.CommandSpecifier}bank squashjournal"));
				}

                if (args.Player.Group.HasPermission("bank.reset"))
                {
                    args.Player.SendInfoMessage( $"* Reset everyone's bank account with {TShock.Config.Settings.CommandSpecifier}bank reset <player>");
                }
                if (args.Player.Group.HasPermission("bank.leaderboard"))
                {
                    args.Player.SendInfoMessage( $"* Show the leaderboard for everyone's balance with {TShock.Config.Settings.CommandSpecifier}bank lb <Page>");
                }
                return;
			}

			if (args.Parameters[0].Equals("reset", StringComparison.CurrentCultureIgnoreCase)) {
				if (args.Player.Group.HasPermission("bank.reset")) {
					if(args.Parameters.Count == 1)
					{
						args.Player.SendErrorMessage("[SEconomy Reset] Invalid subcommand usage.");
                        return;
					}
					if (args.Parameters.Count >= 2 && !string.IsNullOrEmpty(args.Parameters[1])) {
						IBankAccount targetAccount = Parent.RunningJournal.GetBankAccountByName(args.Parameters[1]);

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
			else if (args.Parameters[0].Equals("bal", StringComparison.CurrentCultureIgnoreCase)
				|| args.Parameters[0].Equals("balance", StringComparison.CurrentCultureIgnoreCase)) {


				//The command supports viewing other people's balance if the caller has permission
				if (args.Player.Group.HasPermission("bank.viewothers")) {
					if (args.Parameters.Count >= 2) {
						selectedAccount = Parent.RunningJournal.GetBankAccountByName(args.Parameters[1]);
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
					args.Player.SendInfoMessage("[SEconomy XML] Beginning Squash");
					await Parent.RunningJournal.SquashJournalAsync();
					await Parent.RunningJournal.SaveJournalAsync();
					args.Player.SendInfoMessage("[SEconomy XML] Finished Squash");
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
									string.Format("{0} >> {1}", args.Player.Name, selectedAccount.UserAccountName),
									string.Format("SE: tfr: {0} to {1} for {2}", args.Player.Name, selectedAccount.UserAccountName, amount.ToString()));
								args.Player.SendSuccessMessage("[Bank Pay] Successfully gave {0} to {1}.", amount.ToString(), selectedAccount.UserAccountName);
								var player = TSPlayer.FindByNameOrID(selectedAccount.UserAccountName).FirstOrDefault();
								if (player != null)
									player.SendInfoMessage("[Bank Pay] {0} gave you {1}.", args.Player.Name, amount.ToString());

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
							args.Player.SendErrorMessage( "Cannot find player by the name of {0}.", args.Parameters[1]);
						} else {
							if (Money.TryParse(args.Parameters[2], out amount)) {

								//eliminate a double-negative.  saying "take Player -1p1c" will give them 1 plat 1 copper!
								if (args.Parameters[0].Equals("take", StringComparison.CurrentCultureIgnoreCase) && amount > 0) {
									amount = -amount;
								}

								//Instruct the world bank to give the player money.
								Parent.WorldAccount.TransferTo(selectedAccount, amount, Journal.BankAccountTransferOptions.AnnounceToReceiver, args.Parameters[0] + " command", string.Format("SE: pay: {0} to {1} ", amount.ToString(), args.Parameters[1]));
								var player = TSPlayer.FindByNameOrID(selectedAccount.UserAccountName).FirstOrDefault();
								if (player != null)
								{
									if (args.Parameters[0].Equals("take", StringComparison.CurrentCultureIgnoreCase))
									{
										player.SendInfoMessage("[Bank Take] {0} took {1} from you.", args.Player.Name, amount.ToString());
										args.Player.SendInfoMessage("[Bank Take] successfully took {0} from {1}.", amount.ToString(), player.Name);
									}
									else
									{
										player.SendInfoMessage("[Bank Give] {0} gave you {1} using world account.", args.Player.Name, amount.ToString());
										args.Player.SendInfoMessage("[Bank Give] successfully gave {0} {1} using world account.", player.Name, amount.ToString());
									}
								}

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
			} else if (args.Parameters[0].Equals("lb", StringComparison.CurrentCultureIgnoreCase)
					   || args.Parameters[0].Equals("leaderboard", StringComparison.CurrentCultureIgnoreCase)) {
				
                if (args.Player.Group.HasPermission("bank.leaderboard"))
				{
					if (args.Parameters.Count > 2)
					{
                        args.Player.SendErrorMessage( $"Usage: {TShock.Config.Settings.CommandSpecifier}bank lb <Page>");
						return; 
                    }
					int page = 1;

					if(args.Parameters.Count == 2 && !int.TryParse(args.Parameters[1], out page))
					{
                        args.Player.SendErrorMessage( $"Usage: {TShock.Config.Settings.CommandSpecifier}bank lb <Page>");
                        return;
                    }

					if (page > (int)Math.Ceiling((decimal)Parent.RunningJournal.BankAccounts.Count / 10))
						page = (int)Math.Ceiling((decimal)Parent.RunningJournal.BankAccounts.Count / 10);

					int i = 10 * (page - 1);
					int l = 0;
                    args.Player.SendMessage("[Bank Leaderboard] Heres the leaderboard of everyone's bank:", Microsoft.Xna.Framework.Color.SeaGreen);

                    List<IBankAccount> sortedAccounts = Parent.RunningJournal.BankAccounts.OrderBy(i => -i.Balance).ToList();
					for (; i < sortedAccounts.Count;i++)
					{
						Microsoft.Xna.Framework.Color color;
						string tag;
						int rank = i + 1;

						if (l >= 10)
							break;
						if (sortedAccounts[i].IsSystemAccount)
							continue;

						switch (rank)
						{
							case 1:
								color = Microsoft.Xna.Framework.Color.IndianRed;
								tag = String.Format("[i:{0}]", TShock.Utils.GetItemByName("Luminite Bar")[0].netID);
								break;
							case 2:
							case 3:
								color = Microsoft.Xna.Framework.Color.Goldenrod;
								tag = String.Format("[i:{0}]", TShock.Utils.GetItemByName("Shroomite Bar")[0].netID);
								break;
							case 4:
							case 5:
							case 6:
								color = Microsoft.Xna.Framework.Color.Gold;
                                tag = String.Format("[i:{0}]", TShock.Utils.GetItemByName("Chlorophyte Bar")[0].netID);
								break;
							case 7:
							case 8:
							case 9:
							case 10:
								color = Microsoft.Xna.Framework.Color.Yellow;
								tag = String.Format("[i:{0}]", TShock.Utils.GetItemByName("Hallowed Bar")[0].netID);
                                break;
							case 11:
							case 12:
							case 13:
							case 14:
							case 15:
								color = Microsoft.Xna.Framework.Color.GreenYellow;
                                tag = String.Format("[i:{0}]", TShock.Utils.GetItemByName("Adamantite Bar")[0].netID);
                                break;
							default:
								color = Microsoft.Xna.Framework.Color.SeaGreen;
                                tag = String.Format("[i:{0}]", TShock.Utils.GetItemByName("Palladium Bar")[0].netID);
								break;
                        }
						args.Player.SendMessage(string.Format("[[c/{0}:{1}] - {2} {3} : {4}]", Microsoft.Xna.Framework.Color.Magenta.Hex3(),
												rank.ToString(), tag, sortedAccounts[i].UserAccountName, sortedAccounts[i].Balance), color);
						l++;
					}
                    args.Player.SendMessage(string.Format("Page {0}/{1}",page,(int)Math.Ceiling((decimal)Parent.RunningJournal.BankAccounts.Count / 10)), Microsoft.Xna.Framework.Color.SeaGreen);
                }
				else
				{
                    args.Player.SendErrorMessage("[Bank Leaderboard] You don't have permission to do that.");
                }

			} else {
				args.Player.SendErrorMessage( "[Bank] Invalid subcommand.");
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
