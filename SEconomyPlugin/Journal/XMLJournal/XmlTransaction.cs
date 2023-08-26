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

namespace Wolfje.Plugins.SEconomy.Journal.XMLJournal {
	public class XmlTransaction : ITransaction {
		IBankAccount owningBankAccount;
		Dictionary<string, object> customValues;

		#region "custom keys"

		public const string kXmlTransactionOldTransactonK = "kXmlTransactionOldTransactonK";
		public const string kXmlTransactionOldTransactonFK = "kXmlTransactionOldTransactonK";

		#endregion

		#region "public properties"

		public long BankAccountTransactionK {
			get;
			set;
		}

		public long BankAccountFK {
			get;
			set;
		}

		public Money Amount {
			get;
			set;
		}

		public string Message {
			get;
			set;
		}

		public BankAccountTransactionFlags Flags {
			get;
			set;
		}

		public BankAccountTransactionFlags Flags2 {
			get;
			set;
		}

		public DateTime TransactionDateUtc {
			get;
			set;
		}

		public long BankAccountTransactionFK {
			get;
			set;
		}

		#endregion

		public XmlTransaction(IBankAccount OwningAccount)
		{
			if (OwningAccount == null) {
				throw new ArgumentNullException("OwningAccount is null");
			}
			this.owningBankAccount = OwningAccount;
			this.BankAccountFK = OwningAccount.BankAccountK;
			this.customValues = new Dictionary<string, object>();
		}

		public IBankAccount BankAccount {
			get { return owningBankAccount; }
		}

		public ITransaction OppositeTransaction {
			get { return this.owningBankAccount.OwningJournal.Transactions.FirstOrDefault(i => i.BankAccountTransactionK == this.BankAccountTransactionFK); }
		}

		public Dictionary<string, object> CustomValues {
			get {
				return customValues;
			}
			set {
				customValues = value;
			}
		}

	}
}
