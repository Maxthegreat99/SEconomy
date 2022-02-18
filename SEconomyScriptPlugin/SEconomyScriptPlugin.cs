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
using Jint.Native;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using TerrariaApi.Server;
using Wolfje.Plugins;
using Wolfje.Plugins.Jist;
using Wolfje.Plugins.Jist.Framework;
using Wolfje.Plugins.SEconomy.Journal;

namespace Wolfje.Plugins.SEconomy.SEconomyScriptPlugin {
	[ApiVersion(2, 1)]
	public class SEconomyScriptPlugin : TerrariaPlugin {
		public override string Author { get { return "Wolfje"; } }

		public override string Description { get { return "Provides SEconomy scripting support to Jist"; } }

		public override string Name { get { return "SEconomy Jist Support"; } }

		public override Version Version { get { return Assembly.GetExecutingAssembly().GetName().Version; } }

		public SEconomyScriptPlugin(Terraria.Main game)
			: base(game)
		{

		}

		public override void Initialize()
		{
			JistPlugin.JavascriptFunctionsNeeded += JistPlugin_JavascriptFunctionsNeeded;
		}

		/// <summary>
		/// Tells JIST to submit this class's functions 
		/// to the javascript engine.
		/// </summary>
		private void JistPlugin_JavascriptFunctionsNeeded(object sender, JavascriptFunctionsNeededEventArgs e)
		{
			e.Engine.CreateScriptFunctions(this.GetType(), this);
		}

		/// <summary>
		/// Asynchronously transfers money using SEconomy, then calls back to the JS function specified by completedCallback
		/// </summary>
		[JavascriptFunction("seconomy_transfer_async")]
		public async void SEconomyTransferAsync(Journal.IBankAccount From, Journal.IBankAccount To, Money Amount, string TxMessage, JsValue completedCallback)
		{
			BankTransferEventArgs result = null;
			if (JistPlugin.Instance == null
			    || SEconomyPlugin.Instance == null
			    || From == null
			    || To == null) {
				return;
			}
            
			result = await From.TransferToAsync(To, Amount, 
				Journal.BankAccountTransferOptions.AnnounceToSender, 
				TxMessage, TxMessage);

			if (result == null) {
				result = new BankTransferEventArgs() { TransferSucceeded = false };
			}

			Jist.JistPlugin.Instance.CallFunction(completedCallback, null, result);
		}

		[JavascriptFunction("seconomy_pay_async")]
		public async void SEconomyPayAsync(Journal.IBankAccount From, Journal.IBankAccount To, Money Amount, string TxMessage, JsValue completedCallback)
		{
			BankTransferEventArgs result = null;
			if (JistPlugin.Instance == null
			    || SEconomyPlugin.Instance == null
			    || From == null
			    || To == null) {
				return;
			}
            
			result = await From.TransferToAsync(To, Amount,
				Journal.BankAccountTransferOptions.AnnounceToReceiver
				| Journal.BankAccountTransferOptions.AnnounceToSender
				| Journal.BankAccountTransferOptions.IsPayment,
				TxMessage, TxMessage);

			if (result == null) {
				result = new BankTransferEventArgs() { TransferSucceeded = false };
			}

			Jist.JistPlugin.Instance.CallFunction(completedCallback, null, result);
		}

		/// <summary>
		/// Javascript function:
		///             seconomy_parse_money(money) : Money
		///             
		/// Parses money from a string or object and 
		/// returns a money value.
		/// </summary>
		[JavascriptFunction("seconomy_parse_money")]
		public Money SEconomyParseMoney(object MoneyRep)
		{
			try {
				return Money.Parse(MoneyRep.ToString());
			} catch {
				return 0;
			}
		}

		/// <summary>
		/// Javascript function:
		///             seconomy_valid_money(money) : boolean
		/// Returns true if a supplied money value is valid 
		/// and is parsable or not.
		/// </summary>
		[JavascriptFunction("seconomy_valid_money")]
		public bool SEconomyMoneyValid(object MoneyRep)
		{
			Money _money;
			return Money.TryParse(MoneyRep.ToString(), out _money);
		}


		[JavascriptFunction("seconomy_get_offline_account")]
		public Journal.IBankAccount GetBankAccountOffline(object accountRef)
		{
			if (JistPlugin.Instance == null
				|| SEconomyPlugin.Instance == null) {
				return null;
			}

			if (accountRef is double) {
				return SEconomyPlugin.Instance.RunningJournal.GetBankAccount(Convert.ToInt64((double)accountRef));
			} else if (accountRef is string) {
				return SEconomyPlugin.Instance.RunningJournal.GetBankAccountByName(accountRef as string);
			} else {
				return null;
			}
		}

		/// <summary>
		/// Returns a bank account from a player based on an input object.
		/// </summary>
		[JavascriptFunction("seconomy_get_account")]
		public Journal.IBankAccount GetBankAccount(object PlayerRep)
		{
			Journal.IBankAccount bankAccount = null;
			TShockAPI.TSPlayer player = null;

			if (JistPlugin.Instance == null
			    || SEconomyPlugin.Instance == null
			    || (player = JistPlugin.Instance.stdTshock.GetPlayer(PlayerRep)) == null
			    || (bankAccount = SEconomyPlugin.Instance.GetBankAccount(player)) == null) {
				return null;
			}

			return bankAccount;
		}

		[JavascriptFunction("seconomy_set_multiplier")]
		public bool SetMultiplier(int multi)
		{
			if (SEconomyPlugin.Instance == null) {
				return false;
			}

			SEconomyPlugin.Instance.WorldEc.CustomMultiplier = multi;
			return true;
		}

		[JavascriptFunction("seconomy_get_multiplier")]
		public int GetMultiplier()
		{
			if (SEconomyPlugin.Instance == null) {
				return -1;
			}

			return SEconomyPlugin.Instance.WorldEc.CustomMultiplier;
		}

		/// <summary>
		/// Returns a reference to the currently running SEconomy world account
		/// </summary>
		[JavascriptFunction("seconomy_world_account")]
		public Journal.IBankAccount WorldAccount()
		{
			if (SEconomyPlugin.Instance == null) {
				return null;
			}
			return SEconomyPlugin.Instance.WorldAccount;
		}
	}
}
