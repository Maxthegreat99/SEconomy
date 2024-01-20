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
using Microsoft.Data.Sqlite;
using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TShockAPI;
using TShockAPI.DB;
using TShockAPI.Extensions;

namespace Wolfje.Plugins.SEconomy.Extensions {
	public static class SqlConnectionExtensions {

		public static async Task<int> QueryAsync(this IDbConnection db, string query, params object[] args)
		{
			IDbConnection connection = db.CloneEx();
			IDbCommand command = null;
			int result;

			try {
                connection.Open();
				command = connection.CreateCommand();
				command.CommandText = query;
				command.CommandTimeout = 60;

				for (int i = 0; i < args.Length; i++) {
					command.AddParameter("@" + i, args[i]);
				}

				result = await Task.Run(() => command.ExecuteNonQuery());
			} catch (Exception ex) {
				TShock.Log.ConsoleError("[SEconomy MySQL] QueryAsync error: {0}", ex.Message);
				return -1;
			} finally {
				if (command != null) {
					command.Dispose();
				}
				if (connection != null) {
					connection.Dispose();
				}
			}

			return result;
		}

		public static IDataReader QueryReaderExisting(this IDbConnection db, string query, params object[] args)
		{
			Stopwatch sw = new Stopwatch();
			sw.Start();
			IDataReader reader = null;

			using (var com = db.CreateCommand()) {
				com.CommandText = query;
				com.CommandTimeout = 60;

				for (int i = 0; i < args.Length; i++)
					com.AddParameter("@" + i, args[i]);

				try {
					reader = com.ExecuteReader();
				} catch (Exception ex) {
					TShock.Log.ConsoleError("[SEconomy MySQL] Query error: {0}", ex.Message);
                    
					if (reader != null) {
						reader.Dispose();
					}
				}
			}

			sw.Stop();
			if (sw.Elapsed.TotalSeconds > 10) {
				TShock.Log.ConsoleError("[SEconomy MySQL] Your MySQL server took {0} seconds to respond!\r\nConsider squashing your journal.", sw.Elapsed.TotalSeconds);
			}

			return reader;
		}

		public static int QueryTransaction(this IDbConnection db, IDbTransaction trans, string query, params object[] args)
		{
			Stopwatch sw = new Stopwatch();
			sw.Start();
			int r = 0;

			using (var com = db.CreateCommand()) {
				com.CommandText = query;
				com.Transaction = trans;
				com.CommandTimeout = 60;

				for (int i = 0; i < args.Length; i++) {
					com.AddParameter("@" + i, args[i]);
				}

				try {
					r = com.ExecuteNonQuery();
				} catch (Exception ex) {
					TShock.Log.ConsoleError("[SEconomy MySQL] Query error: {0}", ex.Message);
					r = -1;
				}
			}

			sw.Stop();
			if (sw.Elapsed.TotalSeconds > 10) {
				TShock.Log.ConsoleError("[SEconomy MySQL] Your MySQL server took {0} seconds to respond!\r\nConsider squashing your journal.", sw.Elapsed.TotalSeconds);
			}

			return r;
		}

		/// <summary>
		/// Executes a query on a database and sets identity to the last inserted identity in that table.
		/// </summary>
		/// <param name="olddb">Database to query</param>
		/// <param name="query">Query string with parameters as @0, @1, etc.</param>
		/// <param name="args">Parameters to be put in the query</param>
		/// <returns>Rows affected by query</returns>
		[SuppressMessage("Microsoft.Security", "CA2100:Review SQL queries for security vulnerabilities")]
		public static int QueryIdentity(this MySql.Data.MySqlClient.MySqlConnection olddb, string query, out long identity, params object[] args)
		{
			Stopwatch sw = new Stopwatch();
			int affected = 0;
			sw.Start();
			identity = -1;

			using (var db = new MySql.Data.MySqlClient.MySqlConnection(olddb.ConnectionString)) {
				try {
					db.Open();
					using (var com = db.CreateCommand()) {
						com.CommandText = query;
						com.CommandTimeout = 60;

						for (int i = 0; i < args.Length; i++)
							com.AddParameter("@" + i, args[i]);

						affected = com.ExecuteNonQuery();
						identity = com.LastInsertedId;
					}
				} catch (Exception ex) {
					TShock.Log.ConsoleError("[SEconomy MySQL] Query error: {0}", ex.Message);
					affected = -1;
				}
			}

			sw.Stop();
			if (sw.Elapsed.TotalSeconds > 10) {
				TShock.Log.ConsoleError("[SEconomy MySQL] Your MySQL server took {0} seconds to respond!\r\nConsider squashing your journal.", sw.Elapsed.TotalSeconds);
			}

			return affected;
		}


        /// <summary>
        /// Executes a query on a database and sets identity to the last inserted identity in that table.
        /// </summary>
        /// <param name="olddb">Database to query</param>
        /// <param name="query">Query string with parameters as @0, @1, etc.</param>
        /// <param name="args">Parameters to be put in the query</param>
        /// <returns>Rows affected by query</returns>
        [SuppressMessage("Microsoft.Security", "CA2100:Review SQL queries for security vulnerabilities")]
        public static int QueryIdentity(this SqliteConnection olddb, string query, out long identity, params object[] args)
        {
			int affected = 0;
            identity = -1;

            using (var db = new SqliteConnection(olddb.ConnectionString))
            {
                try
                {
                    db.Open();
                    using (var com = db.CreateCommand())
                    {
                        com.CommandText = query;
                        com.CommandTimeout = 60;

                        for (int i = 0; i < args.Length; i++)
                            com.AddParameter("@" + i, args[i]);

                        affected = com.ExecuteNonQuery();

						using(var searchCmd = db.CreateCommand())
						{
							searchCmd.CommandText = @"SELECT last_insert_rowid()";
							identity = (long)searchCmd.ExecuteScalar();
						}
                    }
                }
                catch (Exception ex)
                {
                    TShock.Log.ConsoleError("[SEconomy MySQL] Query error: {0}", ex.Message);
                    affected = -1;
                }
            }



            return affected;
        }

        public static int QueryIdentityTransaction(this MySql.Data.MySqlClient.MySqlConnection db, MySql.Data.MySqlClient.MySqlTransaction trans, string query, out long identity, params object[] args)
        {
            Stopwatch sw = new Stopwatch();
            int affected = 0;
            sw.Start();

            using (var com = db.CreateCommand())
            {
                com.CommandText = query;
                com.Transaction = trans;
                com.CommandTimeout = 60;

                for (int i = 0; i < args.Length; i++)
                    com.AddParameter("@" + i, args[i]);

                try
                {
                    affected = com.ExecuteNonQuery();
                }
                catch (Exception ex)
                {
                    TShock.Log.ConsoleError("[SEconomy MySQL] Query error: {0}", ex.Message);
                    affected = -1;
                }
                identity = com.LastInsertedId;
            }

            sw.Stop();
            if (sw.Elapsed.TotalSeconds > 10)
            {
                TShock.Log.ConsoleError("[SEconomy MySQL] Your MySQL server took {0} seconds to respond!\r\nConsider squashing your journal.", sw.Elapsed.TotalSeconds);
            }

            return affected;
        }

        public static int QueryIdentityTransaction(this SqliteConnection db, SqliteTransaction trans, string query, out long identity, params object[] args)
        {

            int affected = 0;

            using (var com = db.CreateCommand())
            {
                com.CommandText = query;
                com.Transaction = trans;
                com.CommandTimeout = 60;

                for (int i = 0; i < args.Length; i++)
                    com.AddParameter("@" + i, args[i]);

                try
                {
                    affected = com.ExecuteNonQuery();
                }
                catch (Exception ex)
                {
                    TShock.Log.ConsoleError("[SEconomy MySQL] Query error: {0}", ex.Message);
                    affected = -1;
                }
                using (var searchCmd = db.CreateCommand())
                {
                    searchCmd.CommandText = @"SELECT last_insert_rowid()";
                    identity = (long)searchCmd.ExecuteScalar();
                }
            }


            return affected;
        }

        /// <summary>
        /// Executes a query on a database.
        /// </summary>
        /// <param name="olddb">Database to query</param>
        /// <param name="query">Query string with parameters as @0, @1, etc.</param>
        /// <param name="args">Parameters to be put in the query</param>
        /// <returns>Query result as IDataReader</returns>
        [SuppressMessage("Microsoft.Security", "CA2100:Review SQL queries for security vulnerabilities")]
		public static T QueryScalar<T>(this MySqlConnection olddb, string query, params object[] args)
		{
			Stopwatch sw = new Stopwatch();
			object result = null;
			sw.Start();

			try {
				using (var db = new MySql.Data.MySqlClient.MySqlConnection(olddb.ConnectionString)) {
					db.Open();

					using (var com = db.CreateCommand()) {
						com.CommandText = query;
						com.CommandTimeout = 60;

						for (int i = 0; i < args.Length; i++) {
							com.AddParameter("@" + i, args[i]);
						}

						if ((result = com.ExecuteScalar()) == null) {
							sw.Stop();
							return default(T);
						}

					}
				}
			} catch (Exception ex) {
				TShock.Log.ConsoleError("[SEconomy MySQL] Query error: {0}", ex.Message);
				result = default(T);
			}
			sw.Stop();
			if (sw.Elapsed.TotalSeconds > 10) {
				TShock.Log.ConsoleError("[SEconomy MySQL] Your MySQL server took {0} seconds to respond!\r\nConsider squashing your journal.", sw.Elapsed.TotalSeconds);
			}

			return (T)result;
		}

        /// <summary>
        /// Executes a query on a database.
        /// </summary>
        /// <param name="olddb">Database to query</param>
        /// <param name="query">Query string with parameters as @0, @1, etc.</param>
        /// <param name="args">Parameters to be put in the query</param>
        /// <returns>Query result as IDataReader</returns>
        [SuppressMessage("Microsoft.Security", "CA2100:Review SQL queries for security vulnerabilities")]
        public static T QueryScalar<T>(this SqliteConnection olddb, string query, params object[] args)
        {

            object result = null;

            try
            {
                using (var db = new SqliteConnection(olddb.ConnectionString))
                {
                    db.Open();

                    using (var com = db.CreateCommand())
                    {
                        com.CommandText = query;
                        com.CommandTimeout = 60;

                        for (int i = 0; i < args.Length; i++)
                        {
                            com.AddParameter("@" + i, args[i]);
                        }

                        if ((result = com.ExecuteScalar()) == null)
                        {
                            return default(T);
                        }

                    }
                }
            }
            catch (Exception ex)
            {
                TShock.Log.ConsoleError("[SEconomy MySQL] Query error: {0}", ex.Message);
                result = default(T);
            }

            return (T)result;
        }

        public static T QueryScalarExisting<T>(this IDbConnection db, string query, params object[] args)
		{
			Stopwatch sw = new Stopwatch();
			object result = null;
			sw.Start();

			using (var com = db.CreateCommand()) {
				com.CommandText = query;
				com.CommandTimeout = 60;

				for (int i = 0; i < args.Length; i++) {
					com.AddParameter("@" + i, args[i]);
				}

				try {
					if ((result = com.ExecuteScalar()) == null) {
						sw.Stop();
						return default(T);
					}
				} catch (Exception ex) {
					TShock.Log.ConsoleError("[SEconomy MySQL] Query error: {0}", ex.Message);
					result = default(T);
				}
			}

			sw.Stop();
			if (sw.Elapsed.TotalSeconds > 10) {
				TShock.Log.ConsoleError("[SEconomy MySQL] Your MySQL server took {0} seconds to respond!\r\nConsider squashing your journal.", 
					sw.Elapsed.TotalSeconds);
				result = default(T);
			}

			return (T)result;
		}

		public static T QueryScalarTransaction<T>(this IDbConnection db, IDbTransaction trans, string query, params object[] args)
		{
			Stopwatch sw = new Stopwatch();
			object result = null;
			sw.Start();

			using (var com = db.CreateCommand()) {
				com.CommandText = query;
				com.Transaction = trans;
				com.CommandTimeout = 60;

				for (int i = 0; i < args.Length; i++) {
					com.AddParameter("@" + i, args[i]);
				}

				try {
					if ((result = com.ExecuteScalar()) == null) {
						return default(T);
					}
				} catch (Exception ex) {
					TShock.Log.ConsoleError("[SEconomy MySQL] Query error: {0}", ex.Message);
					result = default(T);
				}
			}

			sw.Stop();
			if (sw.Elapsed.TotalSeconds > 10) {
				TShock.Log.ConsoleError("[SEconomy MySQL] Your MySQL server took {0} seconds to respond!\r\nConsider squashing your journal.", sw.Elapsed.TotalSeconds);
			}

			return (T)result;
		}
	}
}
