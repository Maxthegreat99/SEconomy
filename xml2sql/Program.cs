using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using TShockAPI.DB;
using TShockAPI.Extensions;
using MySql.Data.MySqlClient;
using MySql.Data.Common;
using Wolfje.Plugins.SEconomy;
using Wolfje.Plugins.SEconomy.Journal;
using Wolfje.Plugins.SEconomy.Configuration;
using Wolfje.Plugins.SEconomy.Extensions;
using Wolfje.Plugins.SEconomy.Journal.XMLJournal;
using System.Text.RegularExpressions;
using TShockAPI;

namespace xml2sql {
	public class Program {
		public static event EventHandler<JournalLoadingPercentChangedEventArgs> JournalLoadingPercentChanged;
		protected string connectionString = "server={0};user id={1};password={2};command timeout=0;";
		protected SEconomy sec;

		static void Main(string[] args)
		{
			AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;

			Console.ForegroundColor = ConsoleColor.Cyan;
			Console.WriteLine(" xml2sql - an XML to SQL journal importer");
			Console.WriteLine(" For use with SEconomy Update 15 or newer");
			Console.WriteLine();

			new Program().Process();
		}

		protected MySql.Data.MySqlClient.MySqlConnection Connection { 
			get {
				return new MySqlConnection(string.Format(connectionString, sec.Configuration.SQLConnectionProperties.DbHost,
					sec.Configuration.SQLConnectionProperties.DbUsername, sec.Configuration.SQLConnectionProperties.DbPassword)
					+ ";database=" + sec.Configuration.SQLConnectionProperties.DbName);
			}
		}

		protected MySql.Data.MySqlClient.MySqlConnection ConnectionNoCatalog { 
			get {
				return new MySqlConnection(string.Format(connectionString, sec.Configuration.SQLConnectionProperties.DbHost,
					sec.Configuration.SQLConnectionProperties.DbUsername, sec.Configuration.SQLConnectionProperties.DbPassword));
			}
		}


		/// <summary>
		/// Queries the destination MySQL server to determine if there 
		/// is a database by the name matching sqlProperties.DbName set in the
		/// SEconomy configuration file.
		/// </summary>
		/// <returns>True if the database exists, false otherwise.</returns>
		protected bool DatabaseExists()
		{
			long schemaCount = default(long);
			string query = @"select count(`schema_name`) 
							from `information_schema`.`schemata`
							where `schema_name` = @0";

			if ((schemaCount = ConnectionNoCatalog.QueryScalar<long>(query, sec.Configuration.SQLConnectionProperties.DbName)) > 0) {
				return true;
			}

			return false;
		}

		/// <summary>
		/// Creates an seconomy database in MySQL based on the create database SQL
		/// embedded resources.
		/// </summary>
		protected void CreateDatabase()
		{
			Assembly asm;
			Regex createDbRegex = new Regex(@"create\$(\d+)\.sql");
			Dictionary<int, string> scriptList = new Dictionary<int, string>();
			Match nameMatch = null;
			int scriptIndex = default(int);

			if ((asm = Assembly.GetAssembly(typeof(SEconomyPlugin))) == null) {
				return;
			}

			foreach (string resourceName in asm.GetManifestResourceNames()) {
				if ((nameMatch = createDbRegex.Match(resourceName)).Success == false
					|| int.TryParse(nameMatch.Groups[1].Value, out scriptIndex) == false) {
					continue;
				}

				if (scriptList.ContainsKey(scriptIndex) == false) {
					using (StreamReader sr = new StreamReader(asm.GetManifestResourceStream(resourceName))) {
						scriptList[scriptIndex] = sr.ReadToEnd();
					}
				}
			}

			foreach (string scriptToRun in scriptList.OrderBy(i => i.Key).Select(i => i.Value)) {
				string sql = scriptToRun.Replace("$CATALOG", sec.Configuration.SQLConnectionProperties.DbName);
				ConnectionNoCatalog.Query(sql);
			}
		}

		protected void Process()
		{
			sec = new SEconomy(null);
			SEconomyPlugin.Instance = sec;
			XmlTransactionJournal journal = null;
			int oldPercent = 0, skipped = 0;
			Dictionary<long, long> oldNewTransactions = new Dictionary<long, long>();
			JournalLoadingPercentChangedEventArgs args = new JournalLoadingPercentChangedEventArgs() {
				Label = "Accounts"
			};

			sec.Configuration = Config.FromFile(Config.BaseDirectory + Path.DirectorySeparatorChar + "seconomy.config.json");
			journal = new XmlTransactionJournal(sec, Config.BaseDirectory + Path.DirectorySeparatorChar + "SEconomy.journal.xml.gz");
			
			JournalLoadingPercentChanged += journal_JournalLoadingPercentChanged;
			journal.JournalLoadingPercentChanged += journal_JournalLoadingPercentChanged;
			journal.LoadJournal();

			Console.WriteLine();

			if (DatabaseExists() == false) {
				Console.WriteLine("Your SEconomy database does not exist.  Create it?");
				Console.Write("[y/n] ");
				if (Console.ReadKey().KeyChar != 'y') {
					return;
				}
				CreateDatabase();
			}

			Console.WriteLine("Your SEconomy database will be flushed.  All accounts, and transactions will be deleted before the import.");
			Console.Write("Continue? [y/n] ");

			if (Console.ReadKey().KeyChar != 'y') {
				return;
			}

			Console.WriteLine();

			Connection.Query(string.Format("DELETE FROM `{0}`.`bank_account`;", sec.Configuration.SQLConnectionProperties.DbName));
			Connection.Query(string.Format("ALTER TABLE `{0}`.`bank_account` AUTO_INCREMENT 0;", sec.Configuration.SQLConnectionProperties.DbName));
			Connection.Query(string.Format("DELETE FROM `{0}`.`bank_account_transaction`;", sec.Configuration.SQLConnectionProperties.DbName));
			Connection.Query(string.Format("ALTER TABLE `{0}`.`bank_account_transaction` AUTO_INCREMENT 0;", sec.Configuration.SQLConnectionProperties.DbName));

			Console.WriteLine("This will probably take a while...\r\n");
			Console.WriteLine();
			if (JournalLoadingPercentChanged != null) {
				JournalLoadingPercentChanged(null, args);
			}

			for (int i = 0; i < journal.BankAccounts.Count; i++) {
				IBankAccount account = journal.BankAccounts.ElementAtOrDefault(i);
				double percentComplete = (double)i / (double)journal.BankAccounts.Count * 100;
				long id = -1;
				string query = null;

				if (account == null) {
					continue;
				}

				query = @"INSERT INTO `bank_account` 
							(user_account_name, world_id, flags, flags2, description, old_bank_account_k)
						  VALUES (@0, @1, @2, @3, @4, @5);";

				try {
					Connection.QueryIdentity(query, out id, account.UserAccountName, account.WorldID,
						(int)account.Flags, 0, account.Description, account.BankAccountK);
				} catch (Exception ex) {
					TShock.Log.ConsoleError("[SEconomy MySQL] Sql error adding bank account: " + ex.ToString());
					continue;
				}

				for (int t = 0; t < account.Transactions.Count(); t++) {
					long tranId = -1;
					ITransaction transaction = account.Transactions.ElementAtOrDefault(t);
					string txQuery = @"INSERT INTO `bank_account_transaction` 
										(bank_account_fk, amount, message, flags, flags2, transaction_date_utc, old_bank_account_transaction_k)
										VALUES (@0, @1, @2, @3, @4, @5, @6);";

					try {
						Connection.QueryIdentity(txQuery, out tranId, id, (long)transaction.Amount, transaction.Message,
							(int)BankAccountTransactionFlags.FundsAvailable, (int)transaction.Flags2, transaction.TransactionDateUtc, 
							transaction.BankAccountTransactionK);

						if (oldNewTransactions.ContainsKey(transaction.BankAccountTransactionK) == false) {
							oldNewTransactions[transaction.BankAccountTransactionK] = tranId;
						}
					} catch (Exception ex) {
						TShock.Log.ConsoleError("[SEconomy MySQL] Database error in BeginSourceTransaction: " + ex.Message);
						continue;
					}
				}

				if (oldPercent != (int)percentComplete) {
					args.Percent = (int)percentComplete;
					if (JournalLoadingPercentChanged != null) {
						JournalLoadingPercentChanged(null, args);
					}
					oldPercent = (int)percentComplete;
				}
			}

			args.Label = "Reseed";
			args.Percent = 0;
			if (JournalLoadingPercentChanged != null) {
				JournalLoadingPercentChanged(null, args);
			}

			string updateQuery = @"update bank_account_transaction as OLDT
									inner join (
										select bank_account_transaction_id, old_bank_account_transaction_k
										from bank_account_transaction
									) as NEWT on OLDT.old_bank_account_transaction_k = NEWT.old_bank_account_transaction_k
									set OLDT.bank_account_transaction_fk = NEWT.bank_account_transaction_id";

			Connection.Query(updateQuery);
			Connection.Query("update `bank_account_transaction` set `old_bank_account_transaction_k` = null;");
			args.Percent = 100;
			if (JournalLoadingPercentChanged != null) {
				JournalLoadingPercentChanged(null, args);
			}

			Console.WriteLine("import complete", skipped);
			Console.WriteLine("Press any key to exit");
			Console.Read();
		}

		static void journal_JournalLoadingPercentChanged(object sender, JournalLoadingPercentChangedEventArgs e)
		{
			ConsoleEx.WriteBar(e);
		}

		protected static System.Reflection.Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
		{
			string folderPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + "\\ServerPlugins";
			string assemblyPath = Path.Combine(folderPath, new AssemblyName(args.Name).Name + ".dll");
			if (File.Exists(assemblyPath) == false) return null;
			Assembly assembly = Assembly.LoadFrom(assemblyPath);
			return assembly;
		}
	}
}
