using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Wolfje.Plugins.SEconomy;
using Wolfje.Plugins.SEconomy.Extensions;
using Wolfje.Plugins.SEconomy.CmdAliasModule.Extensions;
using TShockAPI;
using Wolfje.Plugins.SEconomy.Journal;

namespace Wolfje.Plugins.SEconomy.CmdAliasModule {
	public class CmdAlias : IDisposable {
		protected object __rndLock = new object();
		protected readonly Random randomGenerator = new Random();
		protected CmdAliasPlugin parent;

		protected readonly Regex parameterRegex = new Regex(@"\$(\d)(-(\d)?)?");
		protected readonly Regex randomRegex = new Regex(@"\$random\((\d*),(\d*)\)", RegexOptions.IgnoreCase);
		protected readonly Regex runasFunctionRegex = new Regex(@"(\$runas\((.*?),(.*?)\)$)", RegexOptions.IgnoreCase);
		protected readonly Regex msgRegex = new Regex(@"(\$msg\((.*?),(.*?)\)$)", RegexOptions.IgnoreCase);

		/// <summary>

		public Configuration Configuration { get; protected set; }

		public event EventHandler<AliasExecutedEventArgs> AliasExecuted;

		/// Format for this dictionary:
		/// Key: KVP with User's character name, and the command they ran>
		/// Value: UTC datetime when they last ran that command
		/// </summary>
		public readonly Dictionary<KeyValuePair<string, AliasCommand>, DateTime> CooldownList = new Dictionary<KeyValuePair<string, AliasCommand>, DateTime>();

		public CmdAlias(CmdAliasPlugin pluginInstance)
		{
			this.parent = pluginInstance;
			TShockAPI.Commands.ChatCommands.Add(new TShockAPI.Command("aliascmd", ChatCommand_GeneralCommand, "aliascmd") { AllowServer = true });
			Configuration = Configuration.LoadConfigurationFromFile("tshock" + System.IO.Path.DirectorySeparatorChar + "SEconomy" + System.IO.Path.DirectorySeparatorChar + "AliasCmd.config.json");
			ParseCommands();
			AliasExecuted += CmdAliasPlugin_AliasExecuted;
		}

		protected async void ChatCommand_GeneralCommand(TShockAPI.CommandArgs args)
		{
			if (args.Parameters.Count >= 1 && args.Parameters[0].Equals("reload", StringComparison.CurrentCultureIgnoreCase) && args.Player.Group.HasPermission("aliascmd.reloadconfig")) {
				args.Player.SendInfoMessage("aliascmd: Reloading configuration file.");

				try {
					await ReloadConfigAfterDelayAsync(1);
					args.Player.SendInfoMessage("aliascmd: reloading complete.");
				} catch (Exception ex) {
					args.Player.SendErrorMessage("aliascmd: reload failed.  You need to check the server console to find out what went wrong.");
					TShock.Log.ConsoleError("aliascmd reload: Cannot load configuration: {0}", ex.Message);
				}

			} else {
				args.Player.SendErrorMessage("aliascmd: usage: /aliascmd reload: reloads the AliasCmd configuration file.");
			}
		}

		/// <summary>
		/// Asynchronously reparses the AliasCmd configuration file after the specified period.
		/// </summary>
		protected async Task ReloadConfigAfterDelayAsync(int DelaySeconds)
		{
			await Task.Delay(DelaySeconds * 1000);
			TShock.Log.ConsoleInfo("AliasCmd: reloading config.");

			try {
				Configuration reloadedConfig = Configuration.LoadConfigurationFromFile("tshock" + System.IO.Path.DirectorySeparatorChar + "SEconomy" + System.IO.Path.DirectorySeparatorChar + "AliasCmd.config.json");
				Configuration = reloadedConfig;

				ParseCommands();

			} catch (Exception ex) {
				TShock.Log.ConsoleError("aliascmd: Your new config could not be loaded, fix any problems and save the file.  Your old configuration is in effect until this is fixed. \r\n\r\n" + ex.ToString());
				throw;
			}

			TShock.Log.ConsoleInfo("AliasCmd: config reload done.");
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

		protected void CmdAliasPlugin_AliasExecuted(object sender, AliasExecutedEventArgs e)
		{
			//Get the corresponding alias in the config that matches what the user typed.
			foreach (AliasCommand alias in Configuration.CommandAliases.Where(i => i.CommandAlias == e.CommandIdentifier)) {
				DateTime canRunNext = DateTime.MinValue;
				Money commandCost = 0;
				IBankAccount playerAccount;

				if (alias == null || SEconomyPlugin.Instance == null) {
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

					DoCommands(alias, e.CommandArgs.Player, e.CommandArgs.Parameters);
					PopulateCooldownList(cooldownReference);
					return;
				}

                
				if ((playerAccount = SEconomyPlugin.Instance.GetBankAccount(e.CommandArgs.Player)) == null) {
					e.CommandArgs.Player.SendErrorMessage("This command costs money and you don't have a bank account. Please log in first.");
					return;
				}

				if (playerAccount.IsAccountEnabled == false) {
					e.CommandArgs.Player.SendErrorMessage("This command costs money but your bank account is disabled.");
					return;
				}

				if (playerAccount.Balance < commandCost) {
					Money difference = commandCost - playerAccount.Balance;
					e.CommandArgs.Player.SendErrorMessage("This command costs {0}. You need {1} more to be able to use this.", commandCost.ToString(), difference.ToString());
				}

				try {
					//Take money off the player, and indicate that this is a payment for something tangible.
					Journal.BankTransferEventArgs trans = playerAccount.TransferTo(SEconomyPlugin.Instance.WorldAccount, commandCost, Journal.BankAccountTransferOptions.AnnounceToSender | Journal.BankAccountTransferOptions.IsPayment, 
						                                      "", 
						                                      string.Format("AC: {0} cmd {1}", e.CommandArgs.Player.Name, alias.CommandAlias));
					if (trans.TransferSucceeded) {
						DoCommands(alias, e.CommandArgs.Player, e.CommandArgs.Parameters);
						PopulateCooldownList(cooldownReference);
						return;
					}

					e.CommandArgs.Player.SendErrorMessage("Your payment failed.");
				} catch (Exception ex) {
					e.CommandArgs.Player.SendErrorMessage("An error occured in the alias.");
					TShock.Log.ConsoleError("aliascmd error: {0} tried to execute alias {1} which failed with error {2}: {3}", e.CommandArgs.Player.Name, e.CommandIdentifier, ex.Message, ex.ToString());
					return;
				}
			}
		}

		protected void ParseCommands()
		{
			TShockAPI.Commands.ChatCommands.RemoveAll(i => i.Names.Count(x => x.StartsWith("cmdalias.")) > 0);

			foreach (AliasCommand aliasCmd in Configuration.CommandAliases) {
				//The command delegate points to the same function for all aliases, which will generically handle all of them.
				TShockAPI.Command newCommand = new TShockAPI.Command(aliasCmd.Permissions, ChatCommand_AliasExecuted, new string[] {
					aliasCmd.CommandAlias,
					"cmdalias." + aliasCmd.CommandAlias
				}) { AllowServer = true };
				TShockAPI.Commands.ChatCommands.Add(newCommand);
			}
		}

		/// <summary>
		/// Mangles the command to execute with the supplied parameters according to the parameter rules.
		/// </summary>
		/// <param name="parameters">
		///      * Parameter format:
		///      * $1 $2 $3 $4: Takes the individual parameter number from the typed alias and puts it into the commands to execute
		///      * $1-: Takes everything from the indiviual parameter to the end of the line
		///      * $1-3: Take all parameters ranging from the lowest to the highest.
		/// </param>
		protected void ReplaceParameterMarkers(IList<string> parameters, ref string CommandToExecute)
		{
			if (parameterRegex.IsMatch(CommandToExecute)) {

				/* Parameter format:
                 * 
                 * $1 $2 $3 $4: Takes the individual parameter number from the typed alias and puts it into the commands to execute
                 * $1-: Takes everything from the indiviual parameter to the end of the line
                 * $1-3: Take all parameters ranging from the lowest to the highest.
                 */
				foreach (Match match in parameterRegex.Matches(CommandToExecute)) {
					int parameterFrom = !string.IsNullOrEmpty(match.Groups[1].Value) ? int.Parse(match.Groups[1].Value) : 0;
					int parameterTo = !string.IsNullOrEmpty(match.Groups[3].Value) ? int.Parse(match.Groups[3].Value) : 0;
					bool takeMoreThanOne = !string.IsNullOrEmpty(match.Groups[2].Value);
					StringBuilder sb = new StringBuilder();

					//take n
					if (!takeMoreThanOne && parameterFrom > 0) {
						if (parameterFrom <= parameters.Count) {
							sb.Append(parameters[parameterFrom - 1]);
						} else {
							//If the match is put there but no parameter was input, then replace it with nothing.
							sb.Append("");
						}

						//take from n to x
					} else if (takeMoreThanOne && parameterTo > parameterFrom) {
						for (int i = parameterFrom; i <= parameterTo; ++i) {
							if (parameters.Count >= i) {
								sb.Append(" " + parameters[i - 1]);
							}
						}

						//take from n to infinite.
					} else if (takeMoreThanOne && parameterTo == 0) {
						for (int i = parameterFrom; i <= parameters.Count; ++i) {
							sb.Append(" " + parameters[i - 1]);
						}
						//do fuck all lelz
					} else {
						sb.Append("");
					}

					//replace the match expression with the replacement.Oh
					CommandToExecute = CommandToExecute.Replace(match.ToString(), sb.ToString());
				}
			}
		}

		/// <summary>
		/// Executes the AliasCommand.  Will either forward the command to the tshock handler or do something else
		/// </summary>
		protected void DoCommands(AliasCommand alias, TSPlayer player, List<string> parameters)
		{

			//loop through each alias and do the commands.
			foreach (string commandToExecute in alias.CommandsToExecute) {
				//todo: parse paramaters and dynamics
				string mangledString = commandToExecute;

				//specifies whether the command to run should be executed as a command, or ignored.
				//useful for functions like $msg that does other shit
				bool executeCommand = true;

				//replace parameter markers with actual parameter values
				ReplaceParameterMarkers(parameters, ref mangledString);

				mangledString = mangledString.Replace("$calleraccount", player.Name);
				mangledString = mangledString.Replace("$callername", player.Name);

				//$random(x,y) support.  Returns a random number between x and y
				if (randomRegex.IsMatch(mangledString)) {
					foreach (Match match in randomRegex.Matches(mangledString)) {
						int randomFrom = 0;
						int randomTo = 0;

						if (!string.IsNullOrEmpty(match.Groups[2].Value) && int.TryParse(match.Groups[2].Value, out randomTo)
						    && !string.IsNullOrEmpty(match.Groups[1].Value) && int.TryParse(match.Groups[1].Value, out randomFrom)) {

							// this is a critical section
							// Random class is seeded from the system clock if you construct one without a seed.
							// therefore, calls to Next() at exactly the same point in time is likely to produce the same number.
							lock (__rndLock) {
								mangledString = mangledString.Replace(match.ToString(), randomGenerator.Next(randomFrom, randomTo).ToString());
							}
						} else {
							TShock.Log.ConsoleError(match.ToString() + " has some stupid shit in it, have a look at your AliasCmd config file.");
							mangledString = mangledString.Replace(match.ToString(), "");
						}
					}
				}

				// $runas(u,cmd) support.  Run command as user
				if (runasFunctionRegex.IsMatch(mangledString)) {
					foreach (Match match in runasFunctionRegex.Matches(mangledString)) {
						string impersonatedName = match.Groups[2].Value;
						TSPlayer impersonatedPlayer = TShockAPI.TShock.Players.FirstOrDefault(i => i != null && i.Name == impersonatedName);

						if (impersonatedPlayer != null) {
							string commandToRun = match.Groups[3].Value;
							player = impersonatedPlayer;

							mangledString = commandToRun.Trim();
						}
					}
				}

				// $msg(u,msg) support.  Sends the user a non-chat informational message
				if (msgRegex.IsMatch(mangledString)) {

					foreach (Match match in msgRegex.Matches(mangledString)) {
						string msgTarget = match.Groups[2].Value.Trim();
						string message = match.Groups[3].Value.Trim();
						TSPlayer destinationPlayer = TShockAPI.TShock.Players.FirstOrDefault(i => i != null && i.Name == msgTarget);

						if (destinationPlayer != null) {
							//custom command, skip forwarding of the command to the tshock executer
							executeCommand = false;

							destinationPlayer.SendInfoMessage(message);
						}
					}
				}

				//and send the command to tshock to do.
				try {
					//prevent an infinite loop for a subcommand calling the alias again causing a commandloop
					string command = mangledString.Split(' ')[0].Substring(1);
					if (!command.Equals(alias.CommandAlias, StringComparison.CurrentCultureIgnoreCase)) {
						if (executeCommand) {
							player.PermissionlessInvoke(mangledString);
						}
					} else {
						TShock.Log.ConsoleError(string.Format("cmdalias {0}: calling yourself in an alias will cause an infinite loop. Ignoring.", alias.CommandAlias));
					}
				} catch {
					//execute the command disregarding permissions
					player.SendErrorMessage(alias.UsageHelpText);
				}
			}
		}

		/// <summary>
		/// Occurs when someone executes an alias command
		/// </summary>
		protected void ChatCommand_AliasExecuted(TShockAPI.CommandArgs e)
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

				AliasExecuted(null, args);
			}
		}



		public void Dispose()
		{
			Dispose(true);
		}

		protected virtual void Dispose(bool disposing)
		{
			if (disposing) {
				TShockAPI.Commands.ChatCommands.RemoveAll(i => i.Names.Count(x => x.StartsWith("cmdalias.")) > 0);
				AliasExecuted -= CmdAliasPlugin_AliasExecuted;
			}
		}
	}
}
