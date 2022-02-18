extern alias OTAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using System.Text.RegularExpressions;
using TerrariaApi.Server;
using OTAPI.Terraria;
using Wolfje.Plugins.SEconomy;
using Wolfje.Plugins.SEconomy.Extensions;
using Wolfje.Plugins.SEconomy.CmdAliasModule.Extensions;
using System.Threading.Tasks;



namespace Wolfje.Plugins.SEconomy.CmdAliasModule {

	/// <summary>
	/// Provides command aliases that can cost money to execute in SEconomy.
	/// </summary>
	[ApiVersion(2, 1)]
	public class CmdAliasPlugin : TerrariaPlugin {
		protected static CmdAlias aliasCmdInstance;

		public static CmdAlias Instance {
			get {
				return aliasCmdInstance;
			}
		}

		#region "API stub"

		public CmdAliasPlugin(Main game)
			: base(game)
		{
			//we're absolute last in the plugin order.
			Order = int.MaxValue - 1;
		}

		public override string Author {
			get {
				return "Wolfje";
			}
		}

		public override string Description {
			get {
				return "Provides a list of customized command aliases that cost money in SEconomy.";
			}
		}

		public override string Name {
			get {
				return "CmdAlias";
			}
		}

		public override Version Version {
			get {
				return Assembly.GetExecutingAssembly().GetName().Version;
			}
		}

		#endregion

		public override void Initialize()
		{
			aliasCmdInstance = new CmdAlias(this);	
		}
	}
}
