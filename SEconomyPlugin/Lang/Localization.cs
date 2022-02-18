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
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using System.IO;
using System.Text.RegularExpressions;
using System.Reflection;
using TShockAPI;

namespace Wolfje.Plugins.SEconomy.Lang {
	public class Localization {
		public string Locale { get; protected set; }

		public string[] StringTable { get; protected set; }

		public Localization(string Locale)
		{
			this.Locale = Locale;

			if (Load() < 0) {
				//TShock.Log.ConsoleError("SEconomy language loading failed.");
			}
		}

		public static void PrepareLanguages()
		{
			Regex resourceRegex = new Regex(@"\$(.*)$");
			string localeDir = string.Format("{1}{0}Lang{0}", System.IO.Path.DirectorySeparatorChar, Config.BaseDirectory);

			foreach (string resourceName in Assembly.GetExecutingAssembly().GetManifestResourceNames()) {
				string fullResourcePath = null;
				string fileName = null;
				Match fileMatch = null;

				if (resourceName.EndsWith(".xml") == false
				    || resourceRegex.IsMatch(resourceName) == false
				    || (fileMatch = resourceRegex.Match(resourceName)) == null
				    || (fileName = fileMatch.Groups[1].Value) == null) {
					continue;
				}

				fullResourcePath = string.Format("{0}{1}{2}", localeDir, System.IO.Path.DirectorySeparatorChar, fileName);
				try {
					if (System.IO.File.Exists(fullResourcePath) == true) {
						continue;
					}

					using (StreamReader sr = new StreamReader(Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName))) {
						System.IO.File.WriteAllText(fullResourcePath, sr.ReadToEnd());
					}
				} catch (Exception) {
					
				}
			}
		}

		public string StringOrDefault(int index, string defaultText = "")
		{
			string target = null;
			if (this.StringTable == null
			    || (target = this.StringTable.ElementAtOrDefault(index)) == null) {
				target = defaultText;
			}

			return target;
		}

		public int Load()
		{
			XDocument localeDoc = null;
			IEnumerable<XElement> elements = null;
			int tableCount = 0;
			string localePath = string.Format("{1}{0}Lang{0}{2}.xml", System.IO.Path.DirectorySeparatorChar, Config.BaseDirectory, this.Locale);

			if (string.IsNullOrEmpty(Locale) == true) {
				return -1;
			}

			if (File.Exists(localePath) == false) {
				//TShock.Log.ConsoleError("[SEconomy Locale] Language for locale {0} doesn't exist.", Locale);
				return -1;
			}

			try {
				localeDoc = XDocument.Load(localePath);
			} catch {
				//TShock.Log.ConsoleError("Could not load language {0}.", Locale);
				return -1;
			}

			tableCount = localeDoc.Root.Elements().Count();
			this.StringTable = new string[tableCount];
			
			elements = localeDoc.Root.Elements("s");
			for (int i = 0; i < tableCount; i++) {
				XElement stringElement = null;
				if ((stringElement = elements.ElementAtOrDefault(i)) == null) {
					return -1;
				}

				if ((this.StringTable[i] = stringElement.Value) == null) {
					return -1;
				}
			}

			return 0;
		}
	}
}
