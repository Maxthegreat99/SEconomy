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

namespace Wolfje.Plugins.SEconomy.Journal {
	/// <summary>
	/// Describes a bank account transaction
	/// </summary>
	public class BankTransferEventArgs : EventArgs {
		public bool TransferSucceeded { get; set; }

		public IBankAccount ReceiverAccount { get; set; }

		public IBankAccount SenderAccount { get; set; }

		public long TransactionID { get; set; }

		public Money Amount { get; set; }

		public Exception Exception { get; set; }

		public BankAccountTransferOptions TransferOptions { get; set; }

		public string TransactionMessage { get; set; }
	}
}
