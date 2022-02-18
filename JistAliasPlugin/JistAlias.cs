using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TerrariaApi.Server;
using TShockAPI;
using Wolfje.Plugins.SEconomy.CmdAliasModule;
using Wolfje.Plugins.SEconomy.Journal;

namespace Wolfje.Plugins.SEconomy.JistAliasModule {
	public class JistAlias : IDisposable {
		protected readonly JistAliasPlugin parent;
		protected readonly List<JScriptAliasCommand> jsAliases;

		internal event EventHandler<AliasExecutedEventArgs> AliasExecuted;

		/*
         * Standard alias javascript library.
         */
		internal AliasLib.stdalias stdAlias;

		/* 
         * Format for this dictionary:
         * Key: KVP with User's character name, and the command they ran
         * Value: UTC datetime when they last ran that command
         */
		internal readonly Dictionary<KeyValuePair<string, AliasCommand>, DateTime> CooldownList = new Dictionary<KeyValuePair<string, AliasCommand>, DateTime>();

		public JistAlias(JistAliasPlugin plugin)
		{
			this.jsAliases = new List<JScriptAliasCommand>();
			this.parent = plugin;
			this.AliasExecuted += JistAlias_AliasExecuted;
			this.stdAlias = new AliasLib.stdalias(Jist.JistPlugin.Instance, this);
		}

		public void ParseJSCommands()
		{
			lock (jsAliases) {
				foreach (JScriptAliasCommand jCmd in jsAliases) {
					TShockAPI.Command newCommand = new TShockAPI.Command(jCmd.Permissions, ChatCommand_AliasExecuted,
						                               new string[] {
							jCmd.CommandAlias,
							"jistalias." + jCmd.CommandAlias
						}) { AllowServer = true };

					TShockAPI.Commands.ChatCommands.Add(newCommand);
				}
			}
		}

		/// <summary>
		/// Occurs when someone executes an alias command
		/// </summary>
		internal void ChatCommand_AliasExecuted(TShockAPI.CommandArgs e)
		{
			string commandIdentifier = e.Message;

			if (!string.IsNullOrEmpty(e.Message)) {
				commandIdentifier = e.Message.Split(' ').FirstOrDefault();
			}

			if (AliasExecuted != null) {
				AliasExecutedEventArgs args = new AliasExecutedEventArgs() {
					CommandIdentifier = commandIdentifier,
					CommandArgs = e
				};

				AliasExecuted(this, args);
			}
		}

		internal void PopulateCooldownList(KeyValuePair<string, AliasCommand> cooldownReference, TimeSpan? customValue = null)
		{
			//populate the cooldown list.  This dictionary does not go away when people leave so they can't
			//reset cooldowns by simply logging out or disconnecting.  They can reset it however by logging into 
			//a different account.
			DateTime time = DateTime.UtcNow.Add(TimeSpan.FromSeconds(cooldownReference.Value.CooldownSeconds));

			if (customValue.HasValue == true) {
				DateTime.UtcNow.Add(customValue.Value);
			}

			if (CooldownList.ContainsKey(cooldownReference)) {
				CooldownList[cooldownReference] = time;
			} else {
				CooldownList.Add(cooldownReference, time);
			}
		}

		/// <summary>
		/// Gets an alias either by its object reference,
		/// or by it's name if the provided object is a 
		/// string.
		/// </summary>
		internal JScriptAliasCommand GetAlias(object aliasObject)
		{
			string aliasName = null;

			if (aliasObject == null) {
				return null;
			}

			if (aliasObject is JScriptAliasCommand) {
				return aliasObject as JScriptAliasCommand;
			}

			if (aliasObject is string) {
				aliasName = aliasObject as string;
				return jsAliases.FirstOrDefault(i => i.CommandAlias == aliasName);
			}

			return null;
		}

		/// <summary>
		/// Creates a javascript alias pointing to a javascript function.
		/// </summary>
		internal void CreateAlias(JScriptAliasCommand alias, bool allowServer = true)
		{
			TShockAPI.Command newCommand = new TShockAPI.Command(alias.Permissions, ChatCommand_AliasExecuted,
				                               new string[] { 
					alias.CommandAlias, 
					"jistalias." + alias.CommandAlias 
				}) {
				AllowServer = allowServer
			};

			newCommand.DoLog = alias.Silent == false;

			TShockAPI.Commands.ChatCommands.RemoveAll(i => i.Names.Contains("jistalias." + alias.CommandAlias));
			TShockAPI.Commands.ChatCommands.Add(newCommand);
			jsAliases.RemoveAll(i => i.CommandAlias == alias.CommandAlias);
			jsAliases.Add(alias);
		}

		/// <summary>
		/// Removes an alias.
		/// </summary>
		internal void RemoveAlias(JScriptAliasCommand alias)
		{
			jsAliases.RemoveAll(i => i.CommandAlias == alias.CommandAlias);
			TShockAPI.Commands.ChatCommands.RemoveAll(i => i.Names.Contains("jistalias." + alias.CommandAlias));
		}

		/// <summary>
		/// Refunds a player the full cost of an alias.
		/// </summary>
		internal void RefundAlias(Money commandCost, TSPlayer toPlayer)
		{
			if (commandCost == 0
			    || toPlayer == null
			    || SEconomyPlugin.Instance == null) {
				return;
			}
		}


		internal async void JistAlias_AliasExecuted(object sender, AliasExecutedEventArgs e)
		{
			//Get the corresponding alias in the config that matches what the user typed.
			foreach (JScriptAliasCommand alias in jsAliases.Where(i => i.CommandAlias == e.CommandIdentifier)) {
				DateTime canRunNext = DateTime.MinValue;
				Money commandCost = 0;
				IBankAccount account;

				if (alias == null) {
					continue;
				}

				//cooldown key is a pair of the user's character name, and the command they have called.
				KeyValuePair<string, AliasCommand> cooldownReference = new KeyValuePair<string, AliasCommand>(e.CommandArgs.Player.Name, alias);
				if (CooldownList.ContainsKey(cooldownReference)) {
					//UTC time so we don't get any daylight saving shit cuntery
					canRunNext = CooldownList[cooldownReference];
				}

				//has the time elapsed greater than the cooldown period?
				if (DateTime.UtcNow <= canRunNext
				    && e.CommandArgs.Player.Group.HasPermission("aliascmd.bypasscooldown") == false) {
					e.CommandArgs.Player.SendErrorMessage("{0}: You need to wait {1:0} more seconds to be able to use that.",
						alias.CommandAlias,
						canRunNext.Subtract(DateTime.UtcNow).TotalSeconds);
					return;
				}

				if (string.IsNullOrEmpty(alias.Cost) == true
				    || e.CommandArgs.Player.Group.HasPermission("aliascmd.bypasscost") == true
				    || Money.TryParse(alias.Cost, out commandCost) == false
				    || commandCost == 0) {

					if (Jist.JistPlugin.Instance == null) {
						return;
					}

					try {
						/*
                         * Populate cooldown list first before the function's
                         * called, because the function might override it to
                         * something else.
                         */
						PopulateCooldownList(cooldownReference);
						Jist.JistPlugin.Instance.CallFunction(alias.func, alias, e.CommandArgs.Player, e.CommandArgs.Parameters);
						return;
					} catch {
						/*
                         * Command failed.
                         */
					}
				}

				/*
                 * SEconomy may be null.
                 * If this is the case and the command is
                 * not free, bail out.
                 */
				if (SEconomyPlugin.Instance == null) {
					return;
				}

                
				if ((account = SEconomyPlugin.Instance.GetBankAccount(e.CommandArgs.Player)) == null) {
					e.CommandArgs.Player.SendErrorMessage("This command costs money and you don't have a bank account.  Please log in first.");
					return;
				}

				if (account.IsAccountEnabled == false) {
					e.CommandArgs.Player.SendErrorMessage("This command costs money and your account is disabled.");
					return;
				}

				if (account.Balance < commandCost) {
					Money difference = commandCost - account.Balance;
					if (e.CommandArgs.Player == TSPlayer.Server)
					{
						e.CommandArgs.Player.SendErrorMessage("This command costs {0}. You need {1} more to be able to use this.",
						commandCost.ToLongString(),
						difference.ToLongString());
					}
					else
					{
					e.CommandArgs.Player.SendErrorMessage("This command costs {0}. You need {1} more to be able to use this.",
						commandCost.ToString(),
						difference.ToString());
					}
				}

				try {
					/*
                     * Take money off the player, and indicate 
                     * that this is a payment for something 
                     * tangible.
                     */
					Journal.BankTransferEventArgs trans = await account.TransferToAsync(SEconomyPlugin.Instance.WorldAccount,
						                                      commandCost,
						                                      Journal.BankAccountTransferOptions.AnnounceToSender | Journal.BankAccountTransferOptions.IsPayment,
						                                      "",
						                                      string.Format("AC: {0} cmd {1}", e.CommandArgs.Player.Name, alias.CommandAlias));

					if (trans.TransferSucceeded == false) {
						e.CommandArgs.Player.SendErrorMessage("Your payment failed.");
						return;
					}

					if (Jist.JistPlugin.Instance == null) {
						return;
					}

					try {
						/*
                         * Populate cooldown list first before the function's
                         * called, because the function might override it to
                         * something else.
                         */
						PopulateCooldownList(cooldownReference);
						Jist.JistPlugin.Instance.CallFunction(alias.func, alias, e.CommandArgs.Player.Name, e.CommandArgs.Parameters);
					} catch (Exception) {
						/*
                         * Command failed but the person paid money for it.
                         * Refund the full cost of the command if an
                         * exception happens.
                         */
						Jist.ScriptLog.ErrorFormat("alias",
							"{0} paid {1} for alias {2} but it failed and was refunded.",
							e.CommandArgs.Player.Name,
							commandCost.ToString(),
							alias.CommandAlias);
						RefundAlias(commandCost, e.CommandArgs.Player);
					}
				} catch (Exception ex) {
					e.CommandArgs.Player.SendErrorMessage("An error occured in the alias.");
					TShock.Log.ConsoleError("aliascmd error: {0} tried to execute alias {1} which failed with error {2}: {3}",
						e.CommandArgs.Player.Name,
						e.CommandIdentifier,
						ex.Message,
						ex.ToString());
					return;
				}
			}
		}

		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		protected virtual void Dispose(bool disposing)
		{
			if (disposing) {
				stdAlias.Dispose();
				TShockAPI.Commands.ChatCommands.RemoveAll(i => i.Names.Count(x => x.StartsWith("jistalias.")) > 0);
				AliasExecuted -= JistAlias_AliasExecuted;
			}
		}
	}
}
