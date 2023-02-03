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
using System.Xml.Linq;
using System.Xml.XPath;
using System.Linq;
using System.Threading.Tasks;
using Terraria;
using System.IO;
using System.IO.Compression;
using System.Xml;
using System.Globalization;
using System.Threading;
using System.Collections;
using System.Text;
using Wolfje.Plugins.SEconomy.Journal.XMLJournal;
using TShockAPI;

namespace Wolfje.Plugins.SEconomy.Journal.XMLJournal {

    /// <summary>
    /// Holds an XML representation of the SEconomy transaction journal.
    /// </summary>
    public class XmlTransactionJournal : ITransactionJournal {
        public SEconomy SEconomyInstance { get; set; }

        readonly List<IBankAccount> bankAccounts = new List<IBankAccount>();
        internal static long _bankAccountSeed;
        internal static long _transactionSeed;

        internal string path;

        internal System.Timers.Timer JournalBackupTimer { get; set; }

        public event EventHandler<PendingTransactionEventArgs> BankTransactionPending;
        public event EventHandler<BankTransferEventArgs> BankTransferCompleted;
        public event EventHandler<JournalLoadingPercentChangedEventArgs> JournalLoadingPercentChanged;

        /// <summary>
        /// Returns the version of the XML schema built into this dll
        /// </summary>
        public readonly Version XmlSchemaVersion = new Version(1, 3, 0);

        public bool JournalSaving { get; set; }

        public bool BackupsEnabled { get; set; }

        public XmlTransactionJournal(SEconomy Parent, string JournalSavePath)
        {
            this.SEconomyInstance = Parent;
            this.path = JournalSavePath;

            if (Parent != null && Parent.Configuration.JournalBackupMinutes > 0) {
                JournalBackupTimer = new System.Timers.Timer(Parent.Configuration.JournalBackupMinutes * 60000);
                JournalBackupTimer.Elapsed += JournalBackupTimer_Elapsed;
                JournalBackupTimer.Start();
                this.BackupsEnabled = true;
            }
        }

        /// <summary>
        /// Occurs when the journal backup timer fires, and performs a journal save.
        /// </summary>
        protected async void JournalBackupTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            if (this.BackupsEnabled == false
                || this.JournalSaving == true) {
                return;
            }

            await SaveJournalAsync();
        }

        /// <summary>
        /// Returns a world account for the current running world.  If it does not exist, one gets created and then returned.
        /// </summary>
        public IBankAccount GetWorldAccount()
        {
            IBankAccount worldAccount = null;

            //World account matches the current world, ignore.
            if (SEconomyInstance.WorldAccount != null && SEconomyInstance.WorldAccount.WorldID == Main.worldID) {
                return null;
            }

            if (Main.worldID > 0) {
                worldAccount = (from i in bankAccounts
                                where (i.Flags & Journal.BankAccountFlags.SystemAccount) == Journal.BankAccountFlags.SystemAccount
                                    && (i.Flags & Journal.BankAccountFlags.PluginAccount) == 0
                                    && i.WorldID == Main.worldID
                                select i).FirstOrDefault();

                //world account does not exist for this world ID, create one
                if (worldAccount == null) {
                    //This account is always enabled, locked to the world it's in and a system account (ie. can run into deficit) but not a plugin account
                    IBankAccount newWorldAcc = AddBankAccount("SYSTEM", Main.worldID, Journal.BankAccountFlags.Enabled 
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
            }

            return worldAccount;
        }


        /// <summary>
        /// Delfates a GZip byte array and returns the uncompressed data.
        /// </summary>
        MemoryStream GZipDecompress(byte[] CompressedData)
        {
            MemoryStream ms = new MemoryStream();

            using (GZipStream gzStream = new GZipStream(new MemoryStream(CompressedData), CompressionMode.Decompress, false)) {
                byte[] buff = new byte[4096];
                int len = 0;

                do {
                    if ((len = gzStream.Read(buff, 0, 4096)) > 0) {
                        ms.Write(buff, 0, len);
                    }
                } while (len > 0);
            }

            ms.Seek(0, SeekOrigin.Begin);

            return ms;
        }


        public static readonly object __writeLock = new object();
        private static readonly Random _rng = new Random();
        private const string _chars = "1234567890abcdefghijklmnopqrstuvwxyz";

        /// <summary>
        /// Thread-safely generates a random sequence of characters
        /// </summary>
        public static string RandomString(int Size)
        {
            char[] buffer = new char[Size];

            for (int i = 0; i < Size; i++) {
                int charSeed;
                lock (_rng) {
                    charSeed = _rng.Next(_chars.Length);
                }
                buffer[i] = _chars[charSeed];
            }

            return new string(buffer);
        }

        XDocument NewJournal()
        {
            string journalComment = SEconomyPlugin.Locale.StringOrDefault(62);
            string journalAccountComment = SEconomyPlugin.Locale.StringOrDefault(63);
            string journalTransComment = SEconomyPlugin.Locale.StringOrDefault(64);

            return new XDocument(new XDeclaration("1.0", "utf-8", "yes"),
                new XComment(journalComment),
                new XElement("Journal",
                    new XAttribute("Schema", new Version(1, 3, 0).ToString()),
                    new XElement("BankAccounts", new XComment(journalAccountComment))
                ));

        }

        public List<IBankAccount> BankAccounts
        {
            get
            {
                return bankAccounts;
            }
        }

        public IEnumerable<ITransaction> Transactions
        {
            get
            {
                return bankAccounts.SelectMany(i => i.Transactions);
            }
        }

        public IBankAccount AddBankAccount(string UserAccountName, long WorldID, BankAccountFlags Flags, string Description)
        {
            return AddBankAccount(new XmlBankAccount(this) {
                UserAccountName = UserAccountName,
                WorldID = WorldID,
                Flags = Flags,
                Description = Description,
            });
        }

        public IBankAccount AddBankAccount(IBankAccount Account)
        {
            Account.BankAccountK = Interlocked.Increment(ref _bankAccountSeed);

            this.bankAccounts.Add(Account);

            return Account;
        }

        public IBankAccount GetBankAccountByName(string UserAccountName)
        {
            if (bankAccounts == null) {
                return null;
            }
            return bankAccounts.FirstOrDefault(i => i.UserAccountName == UserAccountName);
        }

        public IBankAccount GetBankAccount(long BankAccountK)
        {
            if (bankAccounts == null) {
                return null;
            }

            for (int i = 0; i < bankAccounts.Count; ++i) {
                IBankAccount acct = bankAccounts[i];

                if (acct.BankAccountK == BankAccountK) {
                    return acct;
                }
            }

            return null;
        }

        public IEnumerable<IBankAccount> GetBankAccountList(long BankAccountK)
        {
            if (bankAccounts == null) {
                return null;
            }
            return BankAccounts.Where(i => i.BankAccountK == BankAccountK);
        }

        public void DumpSummary()
        {
            StringBuilder sb = new StringBuilder();

            var qAccounts = from i in bankAccounts
                            group i by i.BankAccountK into g
                            select new {
                                name = g.Key,
                                count = g.Count()
                            };


            foreach (var summary in qAccounts.OrderByDescending(i => i.count)) {
                sb.AppendLine(string.Format("{0},{1}", summary.name, summary.count));
            }

            System.IO.File.WriteAllText(Config.BaseDirectory + System.IO.Path.DirectorySeparatorChar + "test.csv", sb.ToString());
        }

        public async Task DeleteBankAccountAsync(long BankAccountK)
        {
            await Task.Run(() => DeleteBankAccount(BankAccountK));
        }

        public void DeleteBankAccount(long BankAccountK)
        {
            if (bankAccounts == null) {
                return;
            }
            bankAccounts.RemoveAll(i => i.BankAccountK == BankAccountK);
        }

        public void SaveJournal()
        {
            try {
                XDocument journalXml = NewJournal();
                XElement bankAccountRoot = journalXml.Element("Journal").Element("BankAccounts");
                JournalSaving = true;

                foreach (IBankAccount account in BankAccounts) {
                    XElement bankAccountNode = new XElement("BankAccount");

                    bankAccountNode.Add(new XElement("Transactions"));

                    bankAccountNode.SetAttributeValue("UserAccountName", account.UserAccountName);
                    bankAccountNode.SetAttributeValue("WorldID", account.WorldID);
                    bankAccountNode.SetAttributeValue("Flags", account.Flags);
                    bankAccountNode.SetAttributeValue("Description", account.Description);
                    bankAccountNode.SetAttributeValue("BankAccountK", account.BankAccountK);

                    lock (account.Transactions) {
                        foreach (ITransaction transaction in account.Transactions) {
                            XElement transactionNode = new XElement("Transaction");

                            transactionNode.SetAttributeValue("BankAccountTransactionK", transaction.BankAccountTransactionK);
                            transactionNode.SetAttributeValue("BankAccountTransactionFK", transaction.BankAccountTransactionFK);
                            transactionNode.SetAttributeValue("Flags", transaction.Flags);
                            transactionNode.SetAttributeValue("Flags2", transaction.Flags2);
                            transactionNode.SetAttributeValue("Message", transaction.Message);
                            transactionNode.SetAttributeValue("TransactionDateUtc", transaction.TransactionDateUtc.ToString("s", CultureInfo.InvariantCulture));
                            transactionNode.SetAttributeValue("Amount", transaction.Amount.Value);

                            bankAccountNode.Element("Transactions").Add(transactionNode);
                        }
                    }

                    bankAccountRoot.Add(bankAccountNode);
                }

                lock (__writeLock) {
                    try {
                        File.Delete(path + ".bak");
                    } catch {
                        //ignored
                    }

                    //Make a shadow copy of the written file.  This ensures that if a journal does corrupt then they
                    //atleast have a full working copy backed up.w
                    try {
                        if (File.Exists(path) == true) {
                            File.Move(path, path + ".bak");
                        }
                    } catch {
                        TShock.Log.ConsoleError(SEconomyPlugin.Locale.StringOrDefault(65, "[SEconomy Backup] Cannot copy {0} to {1}, shadow backups will not work!"), path, path + ".bak");
                    }

                    Console.WriteLine(SEconomyPlugin.Locale.StringOrDefault(66, "[SEconomy Journal] Writing to disk"));
                    try {
                        using (FileStream fs = new FileStream(path + ".tmp", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None)) {
                            fs.SetLength(0);

                            using (GZipStream gzCompressor = new GZipStream(fs, CompressionMode.Compress)) {
                                using (XmlTextWriter xmlWriter = new XmlTextWriter(gzCompressor, System.Text.Encoding.UTF8)) {
                                    xmlWriter.Formatting = Formatting.None;
                                    journalXml.WriteTo(xmlWriter);
                                }
                            }
                        }
                    } catch {
                        TShock.Log.ConsoleError(SEconomyPlugin.Locale.StringOrDefault(67, "[SEconomy Journal] Saving your journal failed!"));

                        if (File.Exists(path + ".tmp") == true) {
                            try {
                                File.Delete(path + ".tmp");
                            } catch {
                                TShock.Log.ConsoleError(SEconomyPlugin.Locale.StringOrDefault(68, "[SEconomy Journal] Cannot delete temporary file!"));
                                throw;
                            }
                        }
                    }

                    if (File.Exists(path + ".tmp") == true) {
                        try {
                            File.Move(path + ".tmp", path);
                        } catch {
                            TShock.Log.ConsoleError(SEconomyPlugin.Locale.StringOrDefault(68, "[SEconomy Journal] Cannot delete temporary file!"));
                            throw;
                        }
                    }

                    Console.WriteLine(SEconomyPlugin.Locale.StringOrDefault(69, "[SEconomy Journal] Finished backing up."));
                }
            } catch {
                Console.WriteLine(SEconomyPlugin.Locale.StringOrDefault(70, "[SEconomy Journal] There was an error saving your journal.  Make sure you have backups."));
            } finally {
                JournalSaving = false;
            }
        }

        public Task SaveJournalAsync()
        {
            return Task.Factory.StartNew(() => SaveJournal());
        }

        public bool LoadJournal()
        {
            MemoryStream uncompressedData;
            JournalLoadingPercentChangedEventArgs parsingArgs = new JournalLoadingPercentChangedEventArgs() {
                Label = "Loading"
            };
            JournalLoadingPercentChangedEventArgs finalArgs = new JournalLoadingPercentChangedEventArgs() {
                Label = "Verify"
            };

            ConsoleEx.WriteLineColour(ConsoleColor.Cyan, " Using XML journal - {0}", path);
            try {
                byte[] fileData = new byte[0];
            initPoint:
                try {
                    fileData = File.ReadAllBytes(path);
                } catch (Exception ex) {
                    Console.ForegroundColor = ConsoleColor.DarkCyan;

                    if (ex is System.IO.FileNotFoundException || ex is System.IO.DirectoryNotFoundException) {
                        TShock.Log.ConsoleInfo(" * It appears you do not have a journal yet, one will be created for you.");
                        SaveJournal();
                        //yes there are valid uses for goto, don't judge me fool
                        goto initPoint;
                    } else if (ex is System.Security.SecurityException) {
                        TShock.Log.ConsoleError(" * Access denied to the journal file.  Check permissions.");
                    } else {
                        TShock.Log.ConsoleError(" * Loading your journal failed: " + ex.Message);
                    }
                }

                try {
                    uncompressedData = GZipDecompress(fileData);
                } catch {
                    TShock.Log.ConsoleError(" * Decompression failed.");
                    return false;
                }

                if (JournalLoadingPercentChanged != null) {
                    JournalLoadingPercentChanged(this, parsingArgs);
                }

                try {
                    Hashtable bankAccountMap = new Hashtable();
                    Hashtable transactionMap = new Hashtable();

                    //You can't use XDocument.Parse when you have an XDeclaration for some even dumber reason than the write
                    //issue, XDocument has to be constructed from a .net 3.5 XmlReader in this case
                    //or you get a parse exception complaining about literal content.
                    using (XmlTextReader xmlStream = new XmlTextReader(uncompressedData)) {
                        XDocument doc = XDocument.Load(xmlStream);
                        var bankAccountList = doc.XPathSelectElements("/Journal/BankAccounts/BankAccount");
                        int bankAccountCount = bankAccountList.Count();
                        int i = 0;
                        int oldPercent = 0;

                        foreach (XElement elem in bankAccountList) {
                            long bankAccountK = 0L;
                            double percentComplete = (double)i / (double)bankAccountCount * 100;

                            var account = new XmlBankAccount(this) {
                                Description = elem.Attribute("Description").Value,
                                UserAccountName = elem.Attribute("UserAccountName").Value,
                                WorldID = long.Parse(elem.Attribute("WorldID").Value),
                                Flags = (BankAccountFlags)Enum.Parse(typeof(BankAccountFlags), elem.Attribute("Flags").Value)
                            };

                            if (!long.TryParse(elem.Attribute("BankAccountK").Value, out bankAccountK)) {
                                //we've overwritten the old bank account key, add it to the old/new map
                                bankAccountK = Interlocked.Increment(ref _bankAccountSeed);
                                bankAccountMap.Add(elem.Attribute("BankAccountK").Value, bankAccountK);
                            }

                            account.BankAccountK = bankAccountK;

                            //parse transactions under this node
                            if (elem.Element("Transactions") != null) {
                                foreach (XElement txElement in elem.Element("Transactions").Elements("Transaction")) {
                                    long transactionK = 0L;
                                    long transactionFK = 0;
                                    long amount = long.Parse(txElement.Attribute("Amount").Value);
                                    DateTime transactionDate;
                                    BankAccountTransactionFlags flags;

                                    long.TryParse(txElement.Attribute("BankAccountTransactionK").Value, out transactionK);
                                    long.TryParse(txElement.Attribute("BankAccountTransactionFK").Value, out transactionFK);
                                    DateTime.TryParse(txElement.Attribute("TransactionDateUtc").Value, out transactionDate);
                                    Enum.TryParse<BankAccountTransactionFlags>(txElement.Attribute("Flags").Value, out flags);

                                    //ignore orphaned transactions
                                    var trans = new XmlTransaction(account) {
                                        Amount = amount,
                                        BankAccountTransactionK = transactionK,
                                        BankAccountTransactionFK = transactionFK,
                                        TransactionDateUtc = transactionDate,
                                        Flags = flags
                                    };

                                    trans.Message = txElement.Attribute("Message") != null ? txElement.Attribute("Message").Value : null;

                                    account.AddTransaction(trans);
                                }
                            }

                            account.SyncBalance();

                            if (oldPercent != (int)percentComplete) {
                                parsingArgs.Percent = (int)percentComplete;
                                if (JournalLoadingPercentChanged != null) {
                                    JournalLoadingPercentChanged(this, parsingArgs);
                                }
                                oldPercent = (int)percentComplete;
                            }

                            Interlocked.Increment(ref i);

                            bankAccounts.Add(account);
                        }

                        Interlocked.Exchange(ref _bankAccountSeed, bankAccounts.Count() > 0 ? bankAccounts.Max(sum => sum.BankAccountK) : 0);

                        //delete transactions with duplicate IDs

                        var qAccounts = from summary in bankAccounts
                                        group summary by summary.BankAccountK into g
                                        where g.Count() > 1
                                        select new {
                                            name = g.Key,
                                            count = g.Count()
                                        };

                        long[] duplicateAccounts = qAccounts.Select(pred => pred.name).ToArray();

                        int removedAccounts = bankAccounts.RemoveAll(pred => duplicateAccounts.Contains(pred.BankAccountK));

                        if (removedAccounts > 0) {
                            TShock.Log.Warn("[Journal] Removed " + removedAccounts + " accounts with duplicate IDs.");
                        }

                        //transactions in the old schema.
                        int tranCount = doc.XPathSelectElements("/Journal/Transactions/Transaction").Count();
                        i = 0; //reset index

                        foreach (XElement elem in doc.XPathSelectElements("/Journal/Transactions/Transaction")) {
                            //Parallel.ForEach(doc.XPathSelectElements("/Journal/Transactions/Transaction"), (elem) => {
                            double percentComplete = (double)i / (double)tranCount * 100;
                            long bankAccountFK = 0L;
                            long transactionK = 0L;
                            long amount = long.Parse(elem.Attribute("Amount").Value);

                            if (!long.TryParse(elem.Attribute("BankAccountFK").Value, out bankAccountFK)) {
                                if (bankAccountMap.ContainsKey(elem.Attribute("BankAccountFK").Value)) {
                                    Interlocked.Exchange(ref bankAccountFK, (long)bankAccountMap[elem.Attribute("BankAccountFK").Value]);
                                }
                            }

                            IBankAccount bankAccount = GetBankAccount(bankAccountFK);


                            long.TryParse(elem.Attribute("BankAccountTransactionK").Value, out transactionK);

                            //ignore orphaned transactions
                            if (bankAccount != null) {
                                var trans = new XmlTransaction(bankAccount) {
                                    Amount = amount,
                                    BankAccountTransactionK = transactionK
                                };

                                if (elem.Attribute("BankAccountTransactionFK") != null) {
                                    trans.CustomValues.Add(XmlTransaction.kXmlTransactionOldTransactonFK, elem.Attribute("BankAccountTransactionFK").Value);
                                }

                                trans.Message = elem.Attribute("Message") != null ? elem.Attribute("Message").Value : null;

                                bankAccount.AddTransaction(trans);

                                transactionMap.Add(elem.Attribute("BankAccountTransactionK").Value, trans.BankAccountTransactionK);
                            }

                            Interlocked.Increment(ref i);
                        }

                        int txCount = Transactions.Count();
                        int x = 0;

                        finalArgs.Percent = 0;

                        if (JournalLoadingPercentChanged != null) {
                            JournalLoadingPercentChanged(this, finalArgs);
                        }

                        foreach (IBankAccount account in bankAccounts) {
                            foreach (XmlTransaction trans in account.Transactions) {
                                double pcc = (double)x / (double)txCount * 100;

                                //assigns the transactionK according to the hashmap of the old key stored as custom values
                                if (trans.CustomValues.ContainsKey(XmlTransaction.kXmlTransactionOldTransactonFK)) {
                                    object value = transactionMap[trans.CustomValues[XmlTransaction.kXmlTransactionOldTransactonFK]];

                                    trans.BankAccountTransactionFK = value != null ? (long)value : -1L;
                                }
                                if (oldPercent != (int)pcc) {
                                    if (JournalLoadingPercentChanged != null) {
                                        finalArgs.Percent = (int)pcc;
                                        JournalLoadingPercentChanged(this, finalArgs);
                                        oldPercent = (int)pcc;
                                    }
                                }
                                Interlocked.Increment(ref x);

                                trans.CustomValues.Clear();
                                trans.CustomValues = null;
                            }
                        }

                        bankAccountMap = null;
                        transactionMap = null;

                       // CleanJournal(PurgeOptions.RemoveOrphanedAccounts | PurgeOptions.RemoveZeroBalanceAccounts);

                        var accountCount = bankAccounts.Count;
                        Console.WriteLine();
                        Console.ForegroundColor = ConsoleColor.Cyan;
                        Console.WriteLine("\r\n Journal clean: {0} accounts and {1} transactions.", accountCount, Transactions.Count());
                        Console.ResetColor();
                    }
                } catch (Exception ex) {
                    ConsoleEx.WriteAtEnd(2, ConsoleColor.Red, "[{0}]\r\n", SEconomyPlugin.Locale.StringOrDefault(79, "corrupt"));
                    TShock.Log.ConsoleError(ex.ToString());
                    Console.WriteLine(SEconomyPlugin.Locale.StringOrDefault(80, "Your transaction journal appears to be corrupt and transactions have been lost.\n\nYou will start with a clean journal.\nYour old journal file has been move to SEconomy.journal.xml.gz.corrupt"));
                    File.Move(path, path + "." + DateTime.Now.ToFileTime().ToString() + ".corrupt");

                    SaveJournal();

                    //yes there are valid uses for goto, don't judge me fool
                    goto initPoint;
                }
            } finally {
                Console.WriteLine();
            }

            return true;
        }

        public Task<bool> LoadJournalAsync()
        {
            return Task.Factory.StartNew(() => LoadJournal());
        }

        public void BackupJournal()
        {
            SaveJournal();
        }

        public async Task BackupJournalAsync()
        {
            await Task.Factory.StartNew(() => BackupJournal());
        }

        public async Task SquashJournalAsync()
        {
            int bankAccountCount = BankAccounts.Count();
            bool responsibleForTurningBackupsBackOn = false;

            Console.WriteLine(SEconomyPlugin.Locale.StringOrDefault(81, "[SEconomy XML] Beginning Squash"));

            if (SEconomyInstance.RunningJournal.BackupsEnabled == true) {
                SEconomyInstance.RunningJournal.BackupsEnabled = false;
                responsibleForTurningBackupsBackOn = true;
            }

            for (int i = 0; i < bankAccountCount; i++) {
                IBankAccount account = BankAccounts.ElementAtOrDefault(i);
                if (account == null) {
                    continue;
                }

                // Add the squished summary
                ITransaction squash = new XmlTransaction(account) {
                    Amount = account.Transactions.Sum(x => x.Amount),
                    Flags = BankAccountTransactionFlags.FundsAvailable | BankAccountTransactionFlags.Squashed,
                    TransactionDateUtc = DateTime.UtcNow,
                    Message = "Transaction squash"
                };

                account.Transactions.Clear();
                account.AddTransaction(squash);
            }

            //abandon the old journal and assign the squashed one
            Console.WriteLine(SEconomyPlugin.Locale.StringOrDefault(82, "re-syncing online accounts."));

            foreach (TSPlayer player in TShockAPI.TShock.Players) {
                IBankAccount account = null;
                if (player == null
                    || SEconomyPlugin.Instance == null
                    || (account = SEconomyPlugin.Instance.GetBankAccount(player)) == null) {
                    return;
                }
                Console.WriteLine("re-syncing {0}", player.Name);
                await account.SyncBalanceAsync();
            }

            await SaveJournalAsync();
            if (responsibleForTurningBackupsBackOn) {
                /* 
                 * the backups could already have been disabled by something else.  
                 * We don't want to be the ones turning it back on
                 */
                SEconomyInstance.RunningJournal.BackupsEnabled = true;
            }
        }

        #region "transfers"

        bool TransferMaySucceed(IBankAccount FromAccount, IBankAccount ToAccount, Money MoneyNeeded, Journal.BankAccountTransferOptions Options)
        {
            if (FromAccount == null
                || ToAccount == null) {
                return false;
            }

            return FromAccount.IsSystemAccount
            || FromAccount.IsPluginAccount
            || ((Options & Journal.BankAccountTransferOptions.AllowDeficitOnNormalAccount) == Journal.BankAccountTransferOptions.AllowDeficitOnNormalAccount)
            || (FromAccount.Balance >= MoneyNeeded && MoneyNeeded > 0);
        }

        ITransaction BeginSourceTransaction(long BankAccountK, Money Amount, string Message)
        {
            IBankAccount bankAccount = GetBankAccount(BankAccountK);
            ITransaction sourceTran = new XmlTransaction(bankAccount);

            sourceTran.Flags = Journal.BankAccountTransactionFlags.FundsAvailable;
            sourceTran.TransactionDateUtc = DateTime.UtcNow;
            sourceTran.Amount = (Amount * (-1));

            if (!string.IsNullOrEmpty(Message)) {
                sourceTran.Message = Message;
            }

            return bankAccount.AddTransaction(sourceTran);
        }

        ITransaction FinishEndTransaction(long SourceBankTransactionKey, IBankAccount ToAccount, Money Amount, string Message)
        {
            ITransaction destTran = new XmlTransaction(ToAccount);

            destTran.BankAccountFK = ToAccount.BankAccountK;
            destTran.Flags = Journal.BankAccountTransactionFlags.FundsAvailable;
            destTran.TransactionDateUtc = DateTime.UtcNow;
            destTran.Amount = Amount;
            destTran.BankAccountTransactionFK = SourceBankTransactionKey;

            if (!string.IsNullOrEmpty(Message)) {
                destTran.Message = Message;
            }

            return ToAccount.AddTransaction(destTran);
        }

        void BindTransactions(ref ITransaction SourceTransaction, ref ITransaction DestTransaction)
        {
            SourceTransaction.BankAccountTransactionFK = DestTransaction.BankAccountTransactionK;
            DestTransaction.BankAccountTransactionFK = SourceTransaction.BankAccountTransactionK;
        }

        public Task<BankTransferEventArgs> TransferBetweenAsync(IBankAccount FromAccount, IBankAccount ToAccount, Money Amount, BankAccountTransferOptions Options, string TransactionMessage, string JournalMessage)
        {
            return Task.Factory.StartNew(() => TransferBetween(FromAccount, ToAccount, Amount, Options, TransactionMessage, JournalMessage));
        }

        public BankTransferEventArgs TransferBetween(IBankAccount FromAccount, IBankAccount ToAccount, Money Amount, BankAccountTransferOptions Options, string TransactionMessage, string JournalMessage)
        {
            BankTransferEventArgs args = new BankTransferEventArgs();
            Guid profile = Guid.Empty;

            try {
                if (ToAccount != null && TransferMaySucceed(FromAccount, ToAccount, Amount, Options)) {
                    PendingTransactionEventArgs pendingTransaction = new PendingTransactionEventArgs(FromAccount, ToAccount, Amount, Options, TransactionMessage, JournalMessage);

                    if (BankTransactionPending != null) {
                        BankTransactionPending(this, pendingTransaction);
                    }

                    if (pendingTransaction == null) {
                        return args;
                    }

                    args.Amount = pendingTransaction.Amount;
                    args.SenderAccount = pendingTransaction.FromAccount;
                    args.ReceiverAccount = pendingTransaction.ToAccount;
                    args.TransferOptions = Options;
                    args.TransferSucceeded = false;
                    args.TransactionMessage = pendingTransaction.TransactionMessage;


                    if (pendingTransaction.IsCancelled) {
                        return args;
                    }

                    //insert the source negative transaction
                    ITransaction sourceTran = BeginSourceTransaction(FromAccount.BankAccountK, pendingTransaction.Amount, pendingTransaction.JournalLogMessage);
                    if (sourceTran != null) {
                        //insert the destination inverse transaction
                        ITransaction destTran = FinishEndTransaction(sourceTran.BankAccountTransactionK, ToAccount, pendingTransaction.Amount, pendingTransaction.JournalLogMessage);

                        if (destTran != null) {
                            //perform the double-entry binding
                            BindTransactions(ref sourceTran, ref destTran);

                            args.TransactionID = sourceTran.BankAccountTransactionK;

                            //update balances
                            FromAccount.Balance += (Amount * (-1));
                            ToAccount.Balance += Amount;

                            //transaction complete
                            args.TransferSucceeded = true;
                        }
                    }
                } else {
                    args.TransferSucceeded = false;
                    TSPlayer from;

                    if ((from = TShockAPI.TShock.Players.FirstOrDefault(i => i.Name == FromAccount.UserAccountName)) == null) {
                        return args;
                    }


                    /*
                     * concept: ??????
                     * if the amount coming from "this" account is a negative then the "sender account" needs to know the transfer failed.
                     * if the amount coming from "this" acount is a positive then the "reciever account" needs to know the transfer failed.
                     */
                    if (ToAccount.IsSystemAccount == false && ToAccount.IsPluginAccount == false) {
                        if (Amount < 0) {
                            from.SendErrorMessage(SEconomyPlugin.Locale.StringOrDefault(83, "Invalid amount."));
                        } else {
                            from.SendErrorMessage(SEconomyPlugin.Locale.StringOrDefault(84, "You need {0} more to make this payment."), ((Money)(FromAccount.Balance - Amount)).ToString());
                        }
                    }
                }
            } catch (Exception ex) {
                args.Exception = ex;
                args.TransferSucceeded = false;
            }

            if (BankTransferCompleted != null) {
                BankTransferCompleted(this, args);
            }

            return args;
        }

        public void CleanJournal(PurgeOptions options)
        {
            long oldPercent = 0;
            IEnumerable<string> userList = TShock.UserAccounts.GetUserAccounts().Select(i => i.Name);
            List<long> deleteList = new List<long>();
            JournalLoadingPercentChangedEventArgs args = new JournalLoadingPercentChangedEventArgs() {
                Label = "Purge",
                Percent = 0
            };

            if (JournalLoadingPercentChanged != null) {
                JournalLoadingPercentChanged(this, args);
            }

            for (int i = 0; i < this.BankAccounts.Count; i++) {
                double pcc = (double)i / (double)BankAccounts.Count * 100;
                IBankAccount account = bankAccounts.ElementAtOrDefault(i);

                if ((options & PurgeOptions.RemoveOrphanedAccounts) == PurgeOptions.RemoveOrphanedAccounts
                    && userList.Contains(account.UserAccountName) == false) {
                    if (deleteList.Contains(account.BankAccountK) == false) {
                        deleteList.Add(account.BankAccountK);
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

                    DeleteBankAccount(deleteList[i]);

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

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing) {
                if (this.JournalBackupTimer != null) {
                    this.JournalBackupTimer.Stop();
                    this.JournalBackupTimer.Elapsed -= JournalBackupTimer_Elapsed;
                    this.JournalBackupTimer.Dispose();
                }
            }
        }
    }
}

