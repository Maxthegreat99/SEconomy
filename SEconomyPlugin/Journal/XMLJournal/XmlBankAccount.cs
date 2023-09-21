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
using System.Threading;
using System.Threading.Tasks;

namespace Wolfje.Plugins.SEconomy.Journal.XMLJournal {
	public class XmlBankAccount : IBankAccount {
		ITransactionJournal owningJournal;
		List<ITransaction> transactions;

		public XmlBankAccount(ITransactionJournal OwningJournal)
		{
			this.owningJournal = OwningJournal;
			this.transactions = new List<ITransaction>();
		}

		#region "Public Properties"

		public long BankAccountK { get; set; }

		public string OldBankAccountK {
			get;
			set;
		}

		public string UserAccountName {
			get;
			set;
		}

		public long WorldID {
			get;
			set;
		}

		public BankAccountFlags Flags {
			get;
			set;
		}

		public string Description {
			get;
			set;
		}

		public Money Balance {
			get;
			set;
		}

		public bool IsAccountEnabled {
			get { return (this.Flags & Journal.BankAccountFlags.Enabled) == Journal.BankAccountFlags.Enabled; }
		}

		public bool IsSystemAccount {
			get { return (this.Flags & Journal.BankAccountFlags.SystemAccount) == Journal.BankAccountFlags.SystemAccount; }
		}

		public bool IsLockedToWorld {
			get { return (this.Flags & Journal.BankAccountFlags.LockedToWorld) == Journal.BankAccountFlags.LockedToWorld; }
		}

		public bool IsPluginAccount {
			get { return (this.Flags & Journal.BankAccountFlags.PluginAccount) == Journal.BankAccountFlags.PluginAccount; }
		}

		public List<ITransaction> Transactions { get { return transactions; } }

		public ITransactionJournal OwningJournal { get { return owningJournal; } }

		#endregion

		public void SyncBalance()
		{
			Money Sum = 0;

			for(int i = 0;i < Transactions.Count; i++)
			{
				var t = Transactions.ElementAtOrDefault(i);

				if (t == null)
					continue;

				Sum += t.Amount;
			}

			this.Balance = Sum;
		}

		public System.Threading.Tasks.Task SyncBalanceAsync()
		{
			return System.Threading.Tasks.Task.Factory.StartNew(() => SyncBalance());
		}

		public BankTransferEventArgs TransferTo(IBankAccount Account, Money Amount, BankAccountTransferOptions Options, string TransactionMessage, string JournalMessage)
		{
			return owningJournal.TransferBetween(this, Account, Amount, Options, TransactionMessage, JournalMessage);
		}

		public async Task<BankTransferEventArgs> TransferToAsync(int Index, Money Amount, BankAccountTransferOptions Options, string TransactionMessage, string JournalMessage)
		{
			IBankAccount account = SEconomyPlugin.Instance.GetBankAccount(Index);

			return await Task.Factory.StartNew(() => TransferTo(account, Amount, Options, TransactionMessage, JournalMessage));
		}

		public async Task<BankTransferEventArgs> TransferToAsync(IBankAccount ToAccount, Money Amount, BankAccountTransferOptions Options, string TransactionMessage, string JournalMessage)
		{
			return await Task.Factory.StartNew(() => TransferTo(ToAccount, Amount, Options, TransactionMessage, JournalMessage));
		}

		public ITransaction AddTransaction(ITransaction Transaction)
		{
			if (Transaction.BankAccountTransactionK == 0) {
				Transaction.BankAccountTransactionK = Interlocked.Increment(ref XmlTransactionJournal._transactionSeed);
			} else {
				if (Transaction.BankAccountTransactionK > XmlTransactionJournal._transactionSeed) {
					Interlocked.Exchange(ref XmlTransactionJournal._transactionSeed, Transaction.BankAccountTransactionK);
				}
			}

			lock (Transactions) {
				this.transactions.Add(Transaction);
			}

			return Transaction;
		}

		public void ResetAccountTransactions(long BankAccountK)
		{
			lock (Transactions) {
				this.Transactions.Clear();
			}

			this.SyncBalance();
		}

		public Task ResetAccountTransactionsAsync(long BankAccountK)
		{
			return Task.Factory.StartNew(() => ResetAccountTransactions(BankAccountK));
		}

	}
}
