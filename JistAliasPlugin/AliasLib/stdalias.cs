using Jint.Native;
using System;
using System.Collections.Generic;
using Wolfje.Plugins.Jist;
using Wolfje.Plugins.Jist.Framework;
using Wolfje.Plugins.SEconomy.CmdAliasModule;

namespace Wolfje.Plugins.SEconomy.JistAliasModule.AliasLib {
	class stdalias : Wolfje.Plugins.Jist.stdlib.stdlib_base {
		protected JistAlias aliasEngine;

		public stdalias(JistEngine engine, JistAlias aliasEngine)
			: base(engine)
		{
			Provides = "aliascmd";
			this.engine = engine;
			this.aliasEngine = aliasEngine;

			JistPlugin.JavascriptFunctionsNeeded += JistPlugin_JavascriptFunctionsNeeded;
		}

		private void JistPlugin_JavascriptFunctionsNeeded(object sender, JavascriptFunctionsNeededEventArgs e)
		{
			e.Engine.CreateScriptFunctions(this.GetType(), this);
		}

		/// <summary>
		/// Javascript function: 
		///     acmd_create_alias(name, cost, cooldown, 
		///                       permissions, executionFunc) 
		///                       : boolean
		/// 
		/// Creates a javascript TShock command that will 
		/// run the executionFunc when a player types
		/// that command.
		/// </summary>
		[JavascriptFunction("create_alias", "acmd_alias_create")]
		public bool CreateAlias(string AliasName, string Cost, int CooldownSeconds, string Permissions, JsValue func)
		{
			try {
				JistAliasModule.JScriptAliasCommand jAlias = new JistAliasModule.JScriptAliasCommand() {
					CommandAlias = AliasName as string,
					CooldownSeconds = Convert.ToInt32(CooldownSeconds),
					Cost = Cost as string,
					Permissions = Permissions as string,
					func = (Jint.Native.JsValue)func
				};

				aliasEngine.CreateAlias(jAlias);
			} catch (Exception ex) {
				Jist.ScriptLog.ErrorFormat("aliascmd", "CreateAlias failed: " + ex.Message);
				return false;
			}

			return true;
		}

		[JavascriptFunction("acmd_alias_create_silent")]
		public bool CreateAliasSilent(string AliasName, string Cost, int CooldownSeconds, string Permissions, JsValue func)
		{
			try {
				JistAliasModule.JScriptAliasCommand jAlias = new JistAliasModule.JScriptAliasCommand() {
					CommandAlias = AliasName as string,
					CooldownSeconds = Convert.ToInt32(CooldownSeconds),
					Cost = Cost as string,
					Permissions = Permissions as string,
					func = (Jint.Native.JsValue)func,
					Silent = true
				};

				aliasEngine.CreateAlias(jAlias);
			} catch (Exception ex) {
				Jist.ScriptLog.ErrorFormat("aliascmd", "CreateAlias failed: " + ex.Message);
				return false;
			}

			return true;
		}

		[JavascriptFunction("acmd_alias_remove")]
		public bool RemoveAlias(object aliasObject)
		{
			JScriptAliasCommand alias = null;
			if ((alias = aliasEngine.GetAlias(aliasObject)) == null) {
				return false;
			}

			try {
				aliasEngine.RemoveAlias(alias);
			} catch (Exception ex) {
				Jist.ScriptLog.ErrorFormat("aliascmd", "RemoveAlias failed: " + ex.Message);
				return false;
			}

			return true;
		}

		[JavascriptFunction("acmd_cooldown_reset")]
		public bool ResetCooldown(object player, object aliasObject)
		{
			JScriptAliasCommand alias = null;
			TShockAPI.TSPlayer tsPlayer = null;

			if ((alias = aliasEngine.GetAlias(aliasObject)) == null
			    || (tsPlayer = JistPlugin.Instance.stdTshock.GetPlayer(player)) == null) {
				return false;
			}

			KeyValuePair<string, AliasCommand> cooldownReference = 
				new KeyValuePair<string, AliasCommand>(tsPlayer.Name, alias);

			if (aliasEngine.CooldownList.ContainsKey(cooldownReference)) {
				aliasEngine.CooldownList.Remove(cooldownReference);
			}

			return true;
		}

		[JavascriptFunction("acmd_cooldown_set")]
		public bool SetCooldown(object player, object aliasObject, int cooldownSeconds)
		{
			JScriptAliasCommand alias = null;
			TShockAPI.TSPlayer tsPlayer = null;

			if ((alias = aliasEngine.GetAlias(aliasObject)) == null
			    || (tsPlayer = JistPlugin.Instance.stdTshock.GetPlayer(player)) == null) {
				return false;
			}

			KeyValuePair<string, AliasCommand> cooldownReference =
				new KeyValuePair<string, AliasCommand>(tsPlayer.Name, alias);

			aliasEngine.PopulateCooldownList(cooldownReference, TimeSpan.FromSeconds(cooldownSeconds));

			return true;
		}
	}
}
