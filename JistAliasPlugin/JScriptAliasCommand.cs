using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Wolfje.Plugins.SEconomy.CmdAliasModule;

namespace Wolfje.Plugins.SEconomy.JistAliasModule {
	public class JScriptAliasCommand : AliasCommand {
        
		public Jint.Native.JsValue func;

		public bool Silent { get; set; }

		public static JScriptAliasCommand Create(string AliasName, string Cost, int CooldownSeconds, string PermissionNeeded, Jint.Native.JsValue func)
		{
			return new JScriptAliasCommand() {
				CommandAlias = AliasName,
				CommandsToExecute = null,
				CooldownSeconds = CooldownSeconds,
				Permissions = PermissionNeeded,
				func = func
			};
		}

		public static JScriptAliasCommand CreateSilent(string AliasName, string Cost, int CooldownSeconds, string PermissionNeeded, Jint.Native.JsValue func)
		{
			return new JScriptAliasCommand() {
				CommandAlias = AliasName,
				CommandsToExecute = null,
				CooldownSeconds = CooldownSeconds,
				Permissions = PermissionNeeded,
				func = func,
				Silent = true
			};
		}
	}
}
