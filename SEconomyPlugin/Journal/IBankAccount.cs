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

namespace Wolfje.Plugins.SEconomy.Journal {
	public interface IBankAccount {

		ITransactionJournal OwningJournal { get; }

		long BankAccountK { get; set; }

		string OldBankAccountK { get; set; }

		string UserAccountName { get; set; }

		long WorldID { get; set; }

		BankAccountFlags Flags { get; set; }

		string Description { get; set; }

		Money Balance { get; set; }

		bool IsAccountEnabled { get; }

		bool IsSystemAccount { get; }

		bool IsLockedToWorld { get; }

		bool IsPluginAccount { get; }

		List<ITransaction> Transactions { get; }

		ITransaction AddTransaction(ITransaction Transaction);

		void ResetAccountTransactions(long BankAccountK);

		Task ResetAccountTransactionsAsync(long BankAccountK);

		void SyncBalance();

		Task SyncBalanceAsync();

		BankTransferEventArgs TransferTo(IBankAccount Account, Money Amount, BankAccountTransferOptions Options, string TransactionMessage, string JournalMessage);

		Task<BankTransferEventArgs> TransferToAsync(int Index, Money Amount, BankAccountTransferOptions Options, string TransactionMessage, string JournalMessage);

		Task<BankTransferEventArgs> TransferToAsync(IBankAccount ToAccount, Money Amount, BankAccountTransferOptions Options, string TransactionMessage, string JournalMessage);

	}
}
