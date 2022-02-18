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
using System.Text;
using Newtonsoft.Json;
using TShockAPI;

namespace Wolfje.Plugins.SEconomy.Configuration.WorldConfiguration {
	public class WorldConfig {
		public bool MoneyFromNPCEnabled = true;
		public bool MoneyFromBossEnabled = true;
		public bool MoneyFromPVPEnabled = true;

		public bool AnnounceNPCKillGains = true;
		public bool AnnounceBossKillGains = true;
		public bool ShowKillGainsDetailed = true;
		public bool ShowKillGainsOverhead = true;
		public int[] OverheadColor = new int[] { 255, 255, 0 };

		public bool StaticDeathPenalty = false;
		public long StaticPenaltyAmount = 0;
		public List<StaticPenaltyOverride> StaticPenaltyOverrides = new List<StaticPenaltyOverride>();

		public bool KillerTakesDeathPenalty = true;
		public decimal DeathPenaltyPercentValue = 10.0M;

		public decimal MoneyPerDamagePoint = 1.0M;

		public bool IgnoreSpawnedFromStatue = true;
		public List<NPCRewardOverride> Overrides = new List<NPCRewardOverride>();

		public WorldConfig()
		{
			StaticPenaltyOverrides.Add(new StaticPenaltyOverride() { StaticRewardOverride = 0, TShockGroup = "" });
		}

		/// <summary>
		/// Loads a configuration file and deserializes it from JSON
		/// </summary>
		public static WorldConfig LoadConfigurationFromFile(string Path)
		{
			WorldConfig config = null;

			try {
				string fileText = System.IO.File.ReadAllText(Path);
				config = JsonConvert.DeserializeObject<WorldConfig>(fileText);
			} catch (Exception ex) {
				if (ex is System.IO.FileNotFoundException || ex is System.IO.DirectoryNotFoundException) {
					TShock.Log.ConsoleError("[SEconomy WorldConfig] Cannot find file or directory. Creating new one.");
					config = WorldConfig.NewSampleConfiguration();
					config.SaveConfiguration(Path);
				} else if (ex is System.Security.SecurityException) {
					TShock.Log.ConsoleError("[SEconomy WorldConfig] Access denied reading file " + Path);
				} else {
					TShock.Log.ConsoleError("[SEconomy WorldConfig] Error " + ex.ToString());
				}
			}

			return config;
		}

		public static WorldConfig NewSampleConfiguration()
		{
			WorldConfig newConfig = new WorldConfig();
			int[] bannedMobs = new int[] { 1, 49, 74, 46, 85, 67, 55, 63, 58, 21 };

			foreach (int bannedMobID in bannedMobs) {
				newConfig.Overrides.Add(new NPCRewardOverride() {
					NPCID = bannedMobID,
					OverridenMoneyPerDamagePoint = 1.0M // for implementing IgnoreSpawnedFromStatue
				});
			}

			newConfig.StaticPenaltyOverrides.Add(new StaticPenaltyOverride() { StaticRewardOverride = 0, TShockGroup = "" });
			return newConfig;
		}

		public void SaveConfiguration(string Path)
		{
			try {
				string config = JsonConvert.SerializeObject(this, Formatting.Indented);
				System.IO.File.WriteAllText(Path, config);
			} catch (Exception ex) {

				if (ex is System.IO.DirectoryNotFoundException) {
					TShock.Log.ConsoleError("[SEconomy WorldConfig] Save directory not found: " + Path);

				} else if (ex is UnauthorizedAccessException || ex is System.Security.SecurityException) {
					TShock.Log.ConsoleError("[SEconomy WorldConfig] Access is denied to Vault config: " + Path);
				} else {
					TShock.Log.ConsoleError("[SEconomy WorldConfig] Error reading file: " + Path);
					throw;
				}
			}
		}
	}

	public class NPCRewardOverride {
		public int NPCID = 0;
		public decimal OverridenMoneyPerDamagePoint = 1.0M;
	}

	public class StaticPenaltyOverride {
		public string TShockGroup = "group";
		public long StaticRewardOverride = 0;
	}
}
