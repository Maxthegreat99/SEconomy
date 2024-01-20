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

namespace Wolfje.Plugins.SEconomy.Journal.SqliteJournal
{
	public class SqliteTransaction : ITransaction {
		protected IBankAccount bankAccount;

		public SqliteTransaction(IBankAccount bankAccount)
		{
			this.bankAccount = bankAccount;
		}

		#region ITransaction Members

		public long BankAccountTransactionK { get; set; }

		public long BankAccountFK { get; set; }

		public Money Amount { get; set; }

		public string Message { get; set; }

		public BankAccountTransactionFlags Flags { get; set; }

		public BankAccountTransactionFlags Flags2 { get; set; }

		public DateTime TransactionDateUtc { get; set; }

		public long BankAccountTransactionFK { get; set; }

		public IBankAccount BankAccount {
			get { return bankAccount; }
		}

		public ITransaction OppositeTransaction {
			get { return null; }
		}

		public Dictionary<string, object> CustomValues {
			get { return null; }
		}

		#endregion
	}
}
