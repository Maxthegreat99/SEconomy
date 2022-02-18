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
extern alias OTAPI;
using System;
using System.Text;
using System.Reflection;
using OTAPI.Terraria;
using TerrariaApi.Server;
using TShockAPI;
using System.Threading.Tasks;

namespace Wolfje.Plugins.SEconomy {

	/// <summary>
	/// Seconomy for Terraria and TShock.  
	/// 
	/// Copyright (C) Tyler Watson, 2013-2014.
	/// 
	/// API Version 1.1
	/// </summary>
	[ApiVersion(2, 1)]
	public class SEconomyPlugin : TerrariaPlugin {
		public static Lang.Localization Locale { get; private set; }

		protected static string genericErrorMessage = @"SEconomy failed to load and is disabled. You can attempt to fix what's stopping it from starting and relaunch it with /sec start.

You do NOT have to restart the server to issue this command.  Just continue as normal, and issue the command when the game starts.";

		public static event EventHandler SEconomyLoaded;
		public static event EventHandler SEconomyUnloaded;

		#region "API Plugin Stub"

		public override string Author {
			get {
				return "Wolfje";
			}
		}

		public override string Description {
			get {
				return "Provides server-sided currency tools for servers running TShock";
			}
		}

		public override string Name {
			get {
				string s = "SEconomy (Milestone 1) Update " + this.Version.Build;
#if __PREVIEW
				s += " Preview";
#endif

				return s;
			}
		}

		public override Version Version {
			get {
				return Assembly.GetExecutingAssembly().GetName().Version;
			}
		}

		#endregion

		/// <summary>
		/// The loaded instance of SEconomy.  This field can be null.
		/// </summary>
		public static SEconomy Instance { get; set; }

		public SEconomyPlugin(Main Game) : base(Game)
		{
			Order = 20000;
		}

		public override void Initialize()
		{
			Lang.Localization.PrepareLanguages();
			Locale = new Lang.Localization("en-AU");

			PrintIntro();

			TShockAPI.Commands.ChatCommands.Add(new TShockAPI.Command("seconomy.cmd", TShock_CommandExecuted, "seconomy", "sec"));
			try {
				Instance = new SEconomy(this);
				if (Instance.LoadSEconomy() < 0) {
					throw new Exception("LoadSEconomy() failed.");
				}
			} catch {
				Instance = null;
				TShock.Log.ConsoleError(genericErrorMessage); 
			}

			ServerApi.Hooks.GameInitialize.Register(this, (init) =>
			{

			});
		}


		protected void PrintIntro()
		{
            ConsoleColor originalBackColour;

			Console.WriteLine();
			Console.ForegroundColor = ConsoleColor.Cyan;
			Console.Write(" SEconomy Update ");
			Console.ForegroundColor = ConsoleColor.Yellow;
			Console.Write(this.Version.Build);
			Console.ForegroundColor = ConsoleColor.DarkCyan;
			var ApiVersionFor = typeof(SEconomyPlugin).GetCustomAttributes(false)[0] as ApiVersionAttribute;
			Console.Write(" for API {0}.{1}", ApiVersionFor.ApiVersion.Major, ApiVersionFor.ApiVersion.Minor);

#if __PREVIEW
			Console.ForegroundColor = ConsoleColor.Gray;
			Console.Write(" Preview");

			if (this.Version.Revision > 0) {
				Console.Write(" " + this.Version.Revision);
			}
#endif

			Console.WriteLine();
			Console.ForegroundColor = ConsoleColor.Cyan;
			Console.Write(" Copyright (C) Wolfje, 2014-2016 - ");

			Console.ForegroundColor = ConsoleColor.Blue;
			//Console.Write("http://github.com/tylerjwatson/SEconomy");

			Console.WriteLine("\r\n");

            originalBackColour = Console.BackgroundColor;
            Console.ForegroundColor = ConsoleColor.Black;
            Console.BackgroundColor = ConsoleColor.Yellow;
            Console.WriteLine(" SEconomy is free software.  If you paid for it you were scammed.");
            Console.BackgroundColor = originalBackColour;
            Console.WriteLine("\r\n");


#if __PREVIEW
			ConsoleEx.WriteLineColour(ConsoleColor.DarkRed, " This is a preview version of SEconomy.");
			ConsoleEx.WriteLineColour(ConsoleColor.DarkRed, " Things may not work as expected.  Please submit issues at the URL above.");
			Console.WriteLine();
#endif

            Console.ForegroundColor = ConsoleColor.Cyan;
			Console.WriteLine(" Please wait...");
			Console.WriteLine();
			Console.ResetColor();
		}

		public string GetVersionString()
		{
			StringBuilder sb = new StringBuilder("SEconomy Update");
			sb.AppendFormat(" {0}", this.Version.Build);
#if __PREVIEW
			sb.Append(" Preview");

			if (this.Version.Revision > 0) {
				sb.AppendFormat(" {0}", this.Version.Revision);
			}
#endif
			return sb.ToString();
		}

		protected void RaiseUnloadedEvent()
		{
			if (SEconomyUnloaded != null) {
				SEconomyUnloaded(this, new EventArgs());
			}
		}

		protected void RaiseLoadedEvent()
		{
			if (SEconomyLoaded != null) {
				SEconomyLoaded(this, new EventArgs());
			}
		}


		protected async void TShock_CommandExecuted(TShockAPI.CommandArgs args)
		{
			if (args.Parameters.Count == 0) {
				args.Player.SendInfoMessage(string.Format(Locale.StringOrDefault(3, "{0} by Wolfje"), GetVersionString()));
				//args.Player.SendInfoMessage(" * http://plugins.tw.id.au");
				args.Player.SendInfoMessage(Locale.StringOrDefault(5, $" * {TShock.Config.Settings.CommandSpecifier}sec[onomy] reload|rl - Reloads SEconomy"));
				args.Player.SendInfoMessage(Locale.StringOrDefault(6, $" * {TShock.Config.Settings.CommandSpecifier}sec[onomy] stop - Stops and unloads SEconomy"));
				args.Player.SendInfoMessage(Locale.StringOrDefault(7, $" * {TShock.Config.Settings.CommandSpecifier}sec[onomy] start - Starts SEconomy"));
				return;
			}

			if ((args.Parameters[0].Equals("reload", StringComparison.CurrentCultureIgnoreCase)
			    || args.Parameters[0].Equals("rl", StringComparison.CurrentCultureIgnoreCase))
			    && args.Player.Group.HasPermission("seconomy.command.reload") == true) {

				try {
					await Task.Run(async () => {
						if (Instance != null) {
							Instance.Dispose();
							Instance = null;
							RaiseUnloadedEvent();
						}

						try {
							Instance = new SEconomy(this);
							if (Instance.LoadSEconomy() < 0) {
								throw new Exception("LoadSEconomy() failed.");
							}
							await Instance.BindToWorldAsync();
							RaiseLoadedEvent();
						} catch {
							Instance = null;
							TShock.Log.ConsoleError(genericErrorMessage);
							throw;
						}
					});
				} catch {
					RaiseUnloadedEvent();
					args.Player.SendErrorMessage(Locale.StringOrDefault(12, "SEconomy failed to initialize, and will be unavailable for this session."));
					return;
				}
				args.Player.SendSuccessMessage(Locale.StringOrDefault(8, "SEconomy has reloaded."));
			} else if (args.Parameters[0].Equals("stop", StringComparison.CurrentCultureIgnoreCase)
			           && args.Player.Group.HasPermission("seconomy.command.stop") == true) {
				if (Instance == null) {
					args.Player.SendErrorMessage(Locale.StringOrDefault(9, $"[SEconomy Stop] SEconomy has already stopped. Use {TShock.Config.Settings.CommandSpecifier}sec start to start"));
					return;
				}

				await Task.Run(() => {
					Instance.Dispose();
					Instance = null;
				});

				args.Player.SendSuccessMessage(Locale.StringOrDefault(10, "SEconomy has stopped."));
				RaiseUnloadedEvent();
			} else if (args.Parameters[0].Equals("start", StringComparison.CurrentCultureIgnoreCase)
			           && args.Player.Group.HasPermission("seconomy.command.start") == true) {
				if (Instance != null) {
					args.Player.SendErrorMessage(Locale.StringOrDefault(11, $"[SEconomy Stop] SEconomy has already started. Use {TShock.Config.Settings.CommandSpecifier}sec stop to stop."));
					return;
				}
				try {
					await Task.Run(async () => {
						try {
							Instance = new SEconomy(this);
							if (Instance.LoadSEconomy() < 0) {
								throw new Exception("LoadSEconomy() failed.");
							}
							await Instance.BindToWorldAsync();

						} catch {
							RaiseUnloadedEvent();
							Instance = null;
							TShock.Log.ConsoleError(genericErrorMessage);
							throw;
						}
					});
				} catch {
					args.Player.SendErrorMessage(Locale.StringOrDefault(12, "SEconomy failed to initialize, and will be unavailable for this session."));
					return;
				}

				args.Player.SendSuccessMessage(Locale.StringOrDefault(13, "SEconomy has started."));
				RaiseLoadedEvent();
			} else if ((args.Parameters[0].Equals("multi", StringComparison.CurrentCultureIgnoreCase)
			           || args.Parameters[0].Equals("multiplier", StringComparison.CurrentCultureIgnoreCase))
			           && args.Player.Group.HasPermission("seconomy.command.multi") == true) {
				
				RaiseUnloadedEvent();
				int multi = 0;

				if (args.Parameters.Count == 1) {
					args.Player.SendInfoMessage("[SEconomy Multiplier] Reward multiplier: {0}", SEconomyPlugin.Instance.WorldEc.CustomMultiplier);
					return;
				}

				if (int.TryParse(args.Parameters[1], out multi) == false
				    || multi < 0
				    || multi > 100) {
					args.Player.SendErrorMessage($"[SEconomy Multiplier] Syntax: {TShock.Config.Settings.CommandSpecifier}sec multi[plier] 1-100");
					return;
				}

				SEconomyPlugin.Instance.WorldEc.CustomMultiplier = multi;
				args.Player.SendInfoMessage("[SEconomy Multiplier] Multiplier of {0} set successfully.", multi);
			}

		}

		protected override void Dispose(bool disposing)
		{
			if (disposing) {
				if (Instance != null) {
					Instance.Dispose();
					Instance = null;
				}
			}

			base.Dispose(disposing);
		}

	}
}
