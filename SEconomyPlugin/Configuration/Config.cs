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
using Newtonsoft.Json;
using TShockAPI;

namespace Wolfje.Plugins.SEconomy {
	public class Config {
		public static string BaseDirectory = @"tshock" + System.IO.Path.DirectorySeparatorChar + "SEconomy";
		public static string JournalPath = Config.BaseDirectory + System.IO.Path.DirectorySeparatorChar + "SEconomy.journal.xml.gz";
		protected string path;

		public bool BankAccountsEnabled = true;
		public string StartingMoney = "0";

		public int PayIntervalMinutes = 30;
		public int IdleThresholdMinutes = 10;
		public string IntervalPayAmount = "0";
		public string JournalType = "xml";
		public int JournalBackupMinutes = 1;
		public MoneyProperties MoneyConfiguration = new MoneyProperties();
		public Configuration.SQLConnectionProperties SQLConnectionProperties = new Configuration.SQLConnectionProperties();
		public bool EnableProfiler = false;

		public Config(string path)
		{
			this.path = path;
		}

		/// <summary>
		/// Loads a configuration file and deserializes it from JSON
		/// </summary>
		public static Config FromFile(string Path)
		{
			Config config = null;

			if (!System.IO.Directory.Exists(Config.BaseDirectory)) {
				try {
					System.IO.Directory.CreateDirectory(Config.BaseDirectory);
				} catch {
					TShock.Log.ConsoleError("[SEconomy Configuration] Cannot create base directory: {0}", Config.BaseDirectory);
					return null;
				}
			}

			try {
				string fileText = System.IO.File.ReadAllText(Path);
				config = JsonConvert.DeserializeObject<Config>(fileText);
				config.path = Path;
			} catch (Exception ex) {
				if (ex is System.IO.FileNotFoundException || ex is System.IO.DirectoryNotFoundException) {
					TShock.Log.ConsoleError("[SEconomy Configuration] Cannot find file or directory. Creating new one.");
					config = new Config(Path);
					config.SaveConfiguration();
				} else if (ex is System.Security.SecurityException) {
					TShock.Log.ConsoleError("[SEconomy Configuration] Access denied reading file " + Path);
				} else {
					TShock.Log.ConsoleError("[SEconomy Configuration] Error " + ex.ToString());
				}
			}
			return config;
		}

		public void SaveConfiguration()
		{
			try {
				string config = JsonConvert.SerializeObject(this, Formatting.Indented);
				System.IO.File.WriteAllText(path, config);
			} catch (Exception ex) {

				if (ex is System.IO.DirectoryNotFoundException) {
					TShock.Log.ConsoleError("[SEconomy Config] Save directory not found: " + path);

				} else if (ex is UnauthorizedAccessException || ex is System.Security.SecurityException) {
					TShock.Log.ConsoleError("[SEconomy Config] Access is denied to config: " + path);
				} else {
					TShock.Log.ConsoleError("[SEconomy Config] Error reading file: " + path);
					throw;
				}
			}
		}
	}
}
