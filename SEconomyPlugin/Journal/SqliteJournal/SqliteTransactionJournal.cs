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
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using MySql.Data.MySqlClient;
using NuGet.Protocol.Plugins;
using Terraria;
using TShockAPI;
using TShockAPI.DB;
using Wolfje.Plugins.SEconomy.Extensions;

namespace Wolfje.Plugins.SEconomy.Journal.SqliteJournal
{
	public class SqliteTransactionJournal : ITransactionJournal {
		protected List<IBankAccount> bankAccounts;
		protected SqliteConnection sqlConnection;
		protected SEconomy instance;
		protected string dbname;

        public SqliteTransactionJournal(SEconomy instance, string path)
		{

			this.instance = instance;
			this.SEconomyInstance = instance;
			this.dbname = path;
			this.sqlConnection = new SqliteConnection($"Data Source={path};");
        }

		#region ITransactionJournal Members

		public event EventHandler<BankTransferEventArgs> BankTransferCompleted;
		public event EventHandler<PendingTransactionEventArgs> BankTransactionPending;
		public event EventHandler<JournalLoadingPercentChangedEventArgs> JournalLoadingPercentChanged;

		public SEconomy SEconomyInstance { get; set; }

		public bool JournalSaving { get; set; }

		public bool BackupsEnabled { get; set; }

		public bool RunningTransaction { get; private set; }

		public List<IBankAccount> BankAccounts {
			get { return bankAccounts; }
		}

		public IEnumerable<ITransaction> Transactions {
			get { return null; }
		}

		public SqliteConnection Connection {
			get {
				return sqlConnection;
			}
		}

		public SqliteConnection ConnectionNoCatalog {
			get {
				return sqlConnection;
			}
		}

		public IBankAccount AddBankAccount(string UserAccountName, long WorldID, BankAccountFlags Flags, string iDonoLol)
		{
			return AddBankAccount(new SqliteBankAccount(this) {
				UserAccountName = UserAccountName,
				Description = iDonoLol,
				WorldID = WorldID,
				Flags = Flags
			});
		}

		public IBankAccount AddBankAccount(IBankAccount Account)
		{
			long id = 0;
			string query = @"INSERT INTO bank_account
							 (user_account_name, world_id, flags, flags2, description)
						     VALUES (@0, @1, @2, @3, @4);";

			if (string.IsNullOrEmpty(Account.UserAccountName) && !Account.IsSystemAccount) {
				return null;
			}

			try {
				if (Connection.QueryIdentity(query, out id, Account.UserAccountName, Account.WorldID,
					    (int)Account.Flags, 0, Account.Description) < 0) {
					return null;
				}
			} catch (Exception ex) {
				TShock.Log.ConsoleError("[SEconomy Sqlite] Sql error adding bank account: " + ex.ToString());
				return null;
			}

			Account.BankAccountK = id;
            lock (BankAccounts) {
                BankAccounts.Add(Account);
            }

			return Account;
		}

		public IBankAccount GetBankAccountByName(string UserAccountName)
		{
            IBankAccount account;

			if (bankAccounts == null) {
				return null;
			}

            lock (BankAccounts) {
                account = bankAccounts.FirstOrDefault(i => i.UserAccountName == UserAccountName);
            }

            return account;
		}

		public IBankAccount GetBankAccount(long BankAccountK)
		{
            IBankAccount account;

			if (BankAccounts == null) {
				return null;
			}

            lock (BankAccounts) {
                account = BankAccounts.FirstOrDefault(i => i.BankAccountK == BankAccountK);
            }

            return account;
		}

		public IEnumerable<IBankAccount> GetBankAccountList(long BankAccountK)
		{
            IEnumerable<IBankAccount> list;

			if (bankAccounts == null) {
				return null;
			}

            lock (BankAccounts) {
                list = BankAccounts.Where(i => i.BankAccountK == BankAccountK);
            }

            return list;
		}

		
		public async Task DeleteBankAccountAsync(long BankAccountK)
		{
			IBankAccount account = GetBankAccount(BankAccountK);
			int affected = 0;

			try {
				if (account == null
				    || (affected = await Connection.QueryAsync("DELETE FROM bank_account WHERE bank_account_id = @0", BankAccountK)) == 0) {
					return;
				}
			} catch (Exception ex) {
				TShock.Log.ConsoleError("[SEconomy Sqlite] DeleteBankAccount failed: {0}",
					ex.Message);
			}

			if (affected != 1) {
				TShock.Log.ConsoleError("[SEconomy Sqlite] DeleteBankAccount affected {0} rows where it should have only been 1.",
					affected);
				return;
			}

            lock (BankAccounts) {
                BankAccounts.RemoveAll(i => i.BankAccountK == BankAccountK);
            }
		}

		public void SaveJournal()
		{
			return; //stub
		}

		public async Task SaveJournalAsync()
		{
			await Task.FromResult<object>(null); //stub
		}

		protected bool DatabaseExists()
		{
			if (File.Exists(dbname)) {
				return true;
			}

			return false;
		}

		protected void CreateDatabase()
		{
			try
			{
				if(Connection == null)
				{
					this.sqlConnection = new SqliteConnection($"Data Source={Config.SqliteDbPath};");
                }

                sqlConnection.Open();

                using (SqliteCommand command = new SqliteCommand("PRAGMA foreign_keys = ON;", Connection))
                {
                    command.ExecuteNonQuery();
                }

                SqlTableCreator creator = new(sqlConnection, new SqliteQueryCreator());

				creator.EnsureTableStructure(new SqlTable("bank_account",
					new SqlColumn("bank_account_id", MySqlDbType.Int32) { Primary = true, AutoIncrement = true},
					new SqlColumn("user_account_name", MySqlDbType.Text) { NotNull = true, Length = 64 },
					new SqlColumn("world_id", MySqlDbType.Int64) { NotNull = true},
					new SqlColumn("flags", MySqlDbType.Int32) { NotNull = true},
					new SqlColumn("flags2", MySqlDbType.Int32) { NotNull = true},
					new SqlColumn("description", MySqlDbType.Text) { NotNull = true, Length = 512},
					new SqlColumn("old_bank_account_k", MySqlDbType.Int64) {DefaultValue = null }));

				using (SqliteCommand command = new SqliteCommand())
				{
					command.Connection = sqlConnection;
					command.CommandText = @"
                    CREATE TABLE IF NOT EXISTS bank_account_transaction (
                        bank_account_transaction_id INTEGER PRIMARY KEY AUTOINCREMENT,
                        bank_account_transaction_fk INTEGER DEFAULT NULL,
                        bank_account_fk INTEGER NOT NULL,
                        amount INTEGER NOT NULL,
                        message TEXT DEFAULT NULL,
                        flags INTEGER NOT NULL,
                        flags2 INTEGER NOT NULL,
                        transaction_date_utc DATETIME NOT NULL,
                        old_bank_account_transaction_k INTEGER DEFAULT NULL,
						FOREIGN KEY (bank_account_fk) REFERENCES bank_account (bank_account_id) ON DELETE CASCADE ON UPDATE NO ACTION
                    );
					";

					command.ExecuteNonQuery();
				}
				sqlConnection.Close();
			
			}
			catch(Exception e){
				Console.WriteLine(e.Message + "\n" + e.StackTrace);
			}
		}

		public bool LoadJournal()
		{
			string readKey = null;

			ConsoleEx.WriteLineColour(ConsoleColor.Cyan, " Using Sqlite journal - {0}\r\n",
									  dbname);
			CreateDatabase();

			LoadBankAccounts();

			return true;
		}

		protected void CreateSchema()
		{
			try {
				CreateDatabase();
			} catch (Exception ex) {
				TShock.Log.ConsoleError(" Your SEconomy database does not exist and it couldn't be created.");
				TShock.Log.ConsoleError(" The error was: {0}", ex.Message);
				throw;
			}
		}

		public Task<bool> LoadJournalAsync()
		{
			return Task.Run(() => LoadJournal());
		}

		protected void LoadBankAccounts()
		{
			long bankAccountCount = 0, tranCount = 0;
			int index = 0, oldPercent = 0;
			double percentComplete = 0;
			JournalLoadingPercentChangedEventArgs parsingArgs = new JournalLoadingPercentChangedEventArgs() {
				Label = "Loading"
			};

			try {
				if (JournalLoadingPercentChanged != null) {
					JournalLoadingPercentChanged(this, parsingArgs);
				}

				bankAccounts = new List<IBankAccount>();
				bankAccountCount = Connection.QueryScalar<long>("SELECT COUNT(*) FROM bank_account;");
                tranCount = Connection.QueryScalar<long>("SELECT COUNT(*) FROM bank_account_transaction;");

                QueryResult bankAccountResult = Connection.QueryReader(@"SELECT bank_account.*, SUM(bank_account_transaction.amount) AS balance
																		 FROM bank_account
																		 INNER JOIN bank_account_transaction ON bank_account_transaction.bank_account_fk = bank_account.bank_account_id
																		 GROUP BY bank_account.bank_account_id;");

                Action<int> percentCompleteFunc = i => {
					percentComplete = (double)i / (double)bankAccountCount * 100;

					if (oldPercent != (int)percentComplete) {
						parsingArgs.Percent = (int)percentComplete;
						if (JournalLoadingPercentChanged != null) {
							JournalLoadingPercentChanged(this, parsingArgs);
						}
						oldPercent = (int)percentComplete;
					}
				};

				foreach (var acc in bankAccountResult.AsEnumerable()) {
					SqliteBankAccount sqlAccount = null;
					sqlAccount = new SqliteBankAccount(this) {
						BankAccountK = acc.Get<long>("bank_account_id"),
						Description = acc.Get<string>("description"),
						Flags = (BankAccountFlags)Enum.Parse(typeof(BankAccountFlags), acc.Get<int>("flags").ToString()),
						UserAccountName = acc.Get<string>("user_account_name"),
						WorldID = acc.Get<long>("world_id"),
                        Balance = acc.Get<long>("balance")
					};

					//sqlAccount.SyncBalance();
					lock (BankAccounts) {
						BankAccounts.Add(sqlAccount);
					}

					Interlocked.Increment(ref index);
					percentCompleteFunc(index);
				}

				parsingArgs.Percent = 100;
				if (JournalLoadingPercentChanged != null) {
					JournalLoadingPercentChanged(this, parsingArgs);
				}

               // CleanJournal(PurgeOptions.RemoveOrphanedAccounts | PurgeOptions.RemoveZeroBalanceAccounts);

				Console.WriteLine("\r\n");
				ConsoleEx.WriteLineColour(ConsoleColor.Cyan, "[SEconomy Journal Clean] {0} accounts, {1} transactions", BankAccounts.Count(), tranCount);
			} catch (Exception ex) {
				TShock.Log.ConsoleError("[SEconomy Sqlite] Db error in LoadJournal: " + ex.Message);
				throw;
			}
		}

		public void BackupJournal()
		{
			return; //stub
		}

		public async Task BackupJournalAsync()
		{
			await Task.FromResult<object>(null);
		}

		public async Task SquashJournalAsync()
		{
			TShock.Log.ConsoleInfo(	"[SEconomy Sqlite] Squashing accounts.");
			if (await Connection.QueryAsync(@"CREATE TEMPORARY TABLE IF NOT EXISTS seconomy_squash_temp AS
												SELECT bank_account_id, COALESCE(SUM(amount), 0) AS total_amount
												FROM bank_account
												LEFT JOIN bank_account_transaction ON bank_account_id = bank_account_fk
												GROUP BY bank_account_id;

											  DELETE FROM bank_account_transaction;

											  INSERT INTO bank_account_transaction (bank_account_fk, amount, message, flags, flags2, transaction_date_utc)
												SELECT bank_account_id, total_amount, 'Transaction Squash', 3, 0, CURRENT_TIMESTAMP
												FROM seconomy_squash_temp;") < 0) 
			{
				TShock.Log.ConsoleError("[SEconomy Sqlite] Squashing failed.");
			}

			TShock.Log.ConsoleInfo("[SEconomy Sqlite] Re-syncing online accounts");
			for(int i = 0; i < TShock.Players.Count(); i++) {

				var player = TShock.Players.ElementAtOrDefault(i);

				IBankAccount account = null;
				if (player == null
				    || player.Name == null
				    || (account = instance.GetBankAccount(player)) == null) {
					continue;
				}

				await account.SyncBalanceAsync();
			}

			TShock.Log.ConsoleInfo("[SEconomy Sqlite] Squash complete.");
		}

		bool TransferMaySucceed(IBankAccount FromAccount, IBankAccount ToAccount, Money MoneyNeeded, Journal.BankAccountTransferOptions Options)
		{
			if (FromAccount == null || ToAccount == null) {
				return false;
			}

			return ((FromAccount.IsSystemAccount || FromAccount.IsPluginAccount
			|| ((Options & Journal.BankAccountTransferOptions.AllowDeficitOnNormalAccount) == Journal.BankAccountTransferOptions.AllowDeficitOnNormalAccount))
			|| (FromAccount.Balance >= MoneyNeeded && MoneyNeeded > 0));
		}

		ITransaction BeginSourceTransaction(Microsoft.Data.Sqlite.SqliteTransaction SQLTransaction, long BankAccountK, Money Amount, string Message)
		{
			SqliteTransaction trans = null;
			long idenitity = -1;
            string query = @"INSERT INTO bank_account_transaction 
							(bank_account_fk, amount, message, flags, flags2, transaction_date_utc)
							VALUES (@0, @1, @2, @3, @4, @5);";
            IBankAccount account = null;
			if ((account = GetBankAccount(BankAccountK)) == null) {
				return null;
			}
			trans = new SqliteTransaction(account) {
				Amount = (-1) * Amount,
				BankAccountFK = account.BankAccountK,
				Flags = BankAccountTransactionFlags.FundsAvailable,
				Message = Message,
				TransactionDateUtc = DateTime.UtcNow
			};

			try {
				SQLTransaction.Connection.QueryIdentityTransaction(SQLTransaction, query, out idenitity, trans.BankAccountFK, 
					(long)trans.Amount, trans.Message, (int)BankAccountTransactionFlags.FundsAvailable, 0, DateTime.UtcNow);
			} catch (Exception ex) {
				TShock.Log.ConsoleError("[SEconomy Sqlite] Database error in BeginSourceTransaction: " + ex.Message);
				return null;
			}

			trans.BankAccountTransactionK = idenitity;

			return trans;
		}

		ITransaction FinishEndTransaction(Microsoft.Data.Sqlite.SqliteTransaction SqliteTransaction, IBankAccount ToAccount, Money Amount, string Message)
		{
            SqliteTransaction trans = null;
			IBankAccount account = null;
			long identity = -1;
            string query = @"INSERT INTO bank_account_transaction 
							 (bank_account_fk, amount, message, flags, flags2, transaction_date_utc)
							 VALUES (@0, @1, @2, @3, @4, @5);";
            if ((account = GetBankAccount(ToAccount.BankAccountK)) == null) {
				return null;
			}

			trans = new SqliteTransaction(account) {
				Amount = Amount,
				BankAccountFK = account.BankAccountK,
				Flags = BankAccountTransactionFlags.FundsAvailable,
				Message = Message,
				TransactionDateUtc = DateTime.UtcNow
			};

			try {
				SqliteTransaction.Connection.QueryIdentityTransaction(SqliteTransaction, query, out identity, trans.BankAccountFK, (long)trans.Amount, trans.Message,
					(int)BankAccountTransactionFlags.FundsAvailable, 0, DateTime.UtcNow);
			} catch (Exception ex) {
				TShock.Log.ConsoleError("[SEconomy Sqlite] Database error in FinishEndTransaction: " + ex.Message);
				return null;
			}

			trans.BankAccountTransactionK = identity;
			return trans;
		}

		public void BindTransactions(Microsoft.Data.Sqlite.SqliteTransaction SQLTransaction, long SourceBankTransactionK, long DestBankTransactionK)
		{
			int updated = -1;
            string query = @"UPDATE bank_account_transaction 
							 SET bank_account_transaction_fk = @0
							 WHERE bank_account_transaction_id = @1;";

            try {
				if ((updated = SQLTransaction.Connection.QueryTransaction(SQLTransaction, query, SourceBankTransactionK, DestBankTransactionK)) != 1) {
					TShock.Log.ConsoleError("[SEconomy Sqlite]  Error in BindTransactions: updated row count was " + updated);
				}

				if ((updated = SQLTransaction.Connection.QueryTransaction(SQLTransaction, query, DestBankTransactionK, SourceBankTransactionK)) != 1) {
					TShock.Log.ConsoleError("[SEconomy Sqlite]  Error in BindTransactions: updated row count was " + updated);
				}
			} catch (Exception ex) {
				TShock.Log.ConsoleError("[SEconomy Sqlite] Database error in BindTransactions: " + ex.Message);
				return;
			}
		}

		public BankTransferEventArgs TransferBetween(IBankAccount FromAccount, IBankAccount ToAccount, Money Amount, BankAccountTransferOptions Options, string TransactionMessage, string JournalMessage)
		{
			long accountCount = -1;
			PendingTransactionEventArgs pendingTransaction = new PendingTransactionEventArgs(FromAccount, ToAccount, Amount, Options, TransactionMessage, JournalMessage);
			ITransaction sourceTran, destTran;
			SqliteConnection conn = null;
            Microsoft.Data.Sqlite.SqliteTransaction sqlTrans = null;
			BankTransferEventArgs args = new BankTransferEventArgs() {
				TransferSucceeded = false
			};
            string accountVerifyQuery = @"SELECT COUNT(*)
										  FROM bank_account
										  WHERE bank_account_id = @0;";

            Stopwatch sw = new Stopwatch();


			if (SEconomyInstance.Configuration.EnableProfiler == true) {
				sw.Start();
			}
			if (ToAccount == null 
                || TransferMaySucceed(FromAccount, ToAccount, Amount, Options) == false) {
				return args;
			}

			if ((conn = Connection) == null) {
				TShock.Log.ConsoleError("[SEconomy Sqlite] Cannot connect to the SQL server");
				return args;
			}

			conn.Open();

			if ((accountCount = Connection.QueryScalar<long>(accountVerifyQuery, FromAccount.BankAccountK)) != 1) {
				TShock.Log.ConsoleError("[SEconomy Sqlite] Source account " + FromAccount.BankAccountK + " does not exist.");
				conn.Dispose();
				return args;
			}

			if ((accountCount = Connection.QueryScalar<long>(accountVerifyQuery, ToAccount.BankAccountK)) != 1) {
				TShock.Log.ConsoleError("[SEconomy Sqlite] Source account " + FromAccount.BankAccountK + " does not exist.");
				conn.Dispose();
				return args;
			}

			if (BankTransactionPending != null) {
				BankTransactionPending(this, pendingTransaction);
			}

			if (pendingTransaction == null 
                || pendingTransaction.IsCancelled == true) {
				return args;
			}

			args.Amount = pendingTransaction.Amount;
			args.SenderAccount = pendingTransaction.FromAccount;
			args.ReceiverAccount = pendingTransaction.ToAccount;
			args.TransferOptions = Options;
			args.TransactionMessage = pendingTransaction.TransactionMessage;

            try
            {

				while (RunningTransaction)
				{
					Thread.Sleep(25);
				}

				RunningTransaction = true;

                if (conn.State != System.Data.ConnectionState.Open)
                {
                    conn.Open();
                }

                sqlTrans = conn.BeginTransaction();
				if ((sourceTran = BeginSourceTransaction(sqlTrans, FromAccount.BankAccountK, pendingTransaction.Amount, pendingTransaction.JournalLogMessage)) == null) {
					throw new Exception("BeginSourceTransaction failed");
				}

				if ((destTran = FinishEndTransaction(sqlTrans, ToAccount, pendingTransaction.Amount, pendingTransaction.JournalLogMessage)) == null) {
					throw new Exception("FinishEndTransaction failed");
				}

				BindTransactions(sqlTrans, sourceTran.BankAccountTransactionK, destTran.BankAccountTransactionK);
				sqlTrans.Commit();

                RunningTransaction = false;
            } catch (Exception ex) {
				if (conn != null
				    && conn.State == System.Data.ConnectionState.Open) {
					try {
						sqlTrans.Rollback();
					} catch {
						TShock.Log.ConsoleError("[SEconomy Sqlite] Error in rollback:" + ex.ToString());
					}
				}
				TShock.Log.ConsoleError("[SEconomy Sqlite] Database error in transfer:" + ex.ToString());
				args.Exception = ex;

				RunningTransaction = false;

				return args;
			} finally {
				if (conn != null) {
					conn.Dispose();
				}
			}
			
			FromAccount.SyncBalance();
			ToAccount.SyncBalance();

			args.TransferSucceeded = true;
			if (BankTransferCompleted != null) {
				BankTransferCompleted(this, args);
			}

			if (SEconomyInstance.Configuration.EnableProfiler == true) {
				sw.Stop();
				TShock.Log.ConsoleInfo("[SEconomy Sqlite] Transfer took {0} ms", sw.ElapsedMilliseconds);
			}

			return args;
		}

		public async Task<BankTransferEventArgs> TransferBetweenAsync(IBankAccount FromAccount, IBankAccount ToAccount, Money Amount, BankAccountTransferOptions Options, string TransactionMessage, string JournalMessage)
		{
			return await Task.Run(() => TransferBetween(FromAccount, ToAccount, Amount, Options, TransactionMessage, JournalMessage));
		}

		public IBankAccount GetWorldAccount()
		{
			IBankAccount worldAccount = null;

			//World account matches the current world, ignore.
			if ((SEconomyInstance.WorldAccount != null && SEconomyInstance.WorldAccount.WorldID == Main.worldID)
			    || Main.worldID == 0) {
				return null;
			}

            lock (BankAccounts) {
                worldAccount = (from i in bankAccounts
                                where (i.Flags & Journal.BankAccountFlags.SystemAccount) == Journal.BankAccountFlags.SystemAccount
                                    && (i.Flags & Journal.BankAccountFlags.PluginAccount) == 0
                                    && i.WorldID == Main.worldID
                                select i).FirstOrDefault();
            }

			//world account does not exist for this world ID, create one
			if (worldAccount == null) {
				//This account is always enabled, locked to the world it's in and a system account (ie. can run into deficit) but not a plugin account
				IBankAccount newWorldAcc = AddBankAccount("SYSTEM", 
                    Main.worldID, 
                    Journal.BankAccountFlags.Enabled 
                        | Journal.BankAccountFlags.LockedToWorld 
                        | Journal.BankAccountFlags.SystemAccount, 
                    "World account for world " + Main.worldName);

				worldAccount = newWorldAcc;
			}

			if (worldAccount != null) {
				//Is this account listed as enabled?
				bool accountEnabled = (worldAccount.Flags & Journal.BankAccountFlags.Enabled) == Journal.BankAccountFlags.Enabled;

				if (!accountEnabled) {
					TShock.Log.ConsoleError(string.Format(SEconomyPlugin.Locale.StringOrDefault(60, "The world account for world {0} is disabled.  Currency will not work for this game."), Main.worldName));
					return null;
				}
			} else {
				TShock.Log.ConsoleError(SEconomyPlugin.Locale.StringOrDefault(61, "There was an error loading the bank account for this world.  Currency will not work for this game."));
			}

			return worldAccount;
		}

		public void DumpSummary()
		{
			throw new NotImplementedException();
		}

        public void CleanJournal(PurgeOptions options)
        {
            long oldPercent = 0;
            List<string> userList = TShock.UserAccounts.GetUserAccounts().Select(i => i.Name).ToList();
            List<long> deleteList = new List<long>();
            JournalLoadingPercentChangedEventArgs args = new JournalLoadingPercentChangedEventArgs() {
                Label = "Scrub",
                Percent = 0
            };

            if (JournalLoadingPercentChanged != null) {
                JournalLoadingPercentChanged(this, args);
            }

            for (int i = 0; i < this.BankAccounts.Count; i++) {
                double pcc = (double)i / (double)BankAccounts.Count * 100;
                IBankAccount account = this.bankAccounts.ElementAtOrDefault(i);

                if ((options & PurgeOptions.RemoveOrphanedAccounts) == PurgeOptions.RemoveOrphanedAccounts
                    && userList.Contains(account.UserAccountName) == false) {
                    if (deleteList.Contains(account.BankAccountK) == false) {
                        deleteList.Add(account.BankAccountK);
                        userList.Remove(account.UserAccountName);
                        continue;
                    }
                }

                if ((options & PurgeOptions.RemoveZeroBalanceAccounts) == PurgeOptions.RemoveZeroBalanceAccounts
                    && (account.Balance <= 0 && account.IsSystemAccount == false)) {
                    if (deleteList.Contains(account.BankAccountK) == false) {
                        deleteList.Add(account.BankAccountK);
                        continue;
                    }
                }

                if (oldPercent != (int)pcc) {
                    args.Percent = (int)pcc;
                    if (JournalLoadingPercentChanged != null) {
                        JournalLoadingPercentChanged(this, args);
                    }
                    oldPercent = (int)pcc;
                }
            }

            if (deleteList.Count > 0) {
                args.Label = "Clean";
                args.Percent = 0;
                for (int i = 0; i < deleteList.Count; i++) {
                    double pcc = (double)i / (double)deleteList.Count * 100;

                    DeleteBankAccountAsync(deleteList[i]).Wait();

                    if (oldPercent != (int)pcc) {
                        args.Percent = (int)pcc;
                        if (JournalLoadingPercentChanged != null) {
                            JournalLoadingPercentChanged(this, args);
                        }
                        oldPercent = (int)pcc;
                    }
                }
            }
        }
		
		#endregion

		#region IDisposable Members

		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		protected virtual void Dispose(bool disposing)
		{
			if (disposing) {
				sqlConnection.Dispose();
			}
		}

		#endregion
	}
}
