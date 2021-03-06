﻿//  Author:     Jovan Popovic. 
//  This source file is free software, available under MIT license .
//  This source file is distributed in the hope that it will be useful, but
//  WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY
//  or FITNESS FOR A PARTICULAR PURPOSE.See the license files for details.
using System;
using System.Data.Common;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Belgrade.SqlClient.Common
{
    /// <summary>
    /// Component that streams results of SQL query into an output stream.
    /// </summary>
    public class GenericQueryPipe<T> : IQueryPipe
        where T : DbCommand, new()
    {
        /// <summary>
        /// Connection to Sql Database.
        /// </summary>
        private DbConnection Connection;

        /// <summary>
        /// Query mapper used to stream results.
        /// </summary>
        private GenericQueryMapper<T> Mapper;

        /// <summary>
        /// Delegate that is called when some error happens.
        /// </summary>
        Action<Exception> ErrorHandler = null;

        /// <summary>
        /// Creates QueryPipe object.
        /// </summary>
        /// <param name="connection">Connection to Sql Database.</param>
        /// <param name="errorHandler">Function that will be called if some exception is thrown.</param>
        public GenericQueryPipe(DbConnection connection, Action<Exception> errorHandler = null)
        {
            this.Connection = connection;
            this.ErrorHandler = errorHandler?? delegate(Exception ex) { throw ex; };
            this.Mapper = new GenericQueryMapper<T>(connection, this.ErrorHandler);
            
        }

        /// <summary>
        /// Executes SQL query and put results into stream.
        /// </summary>
        /// <param name="sql">SQL query that will be executed.</param>
        /// <param name="stream">Output stream where results will be written.</param>
        /// <param name="defaultOutput">Default content that will be written into stream if there are no results.</param>
        /// <returns>Task</returns>
        public async Task Stream(string sql, Stream stream, string defaultOutput = "")
        {
            using (DbCommand command = new T())
            {
                command.CommandText = sql;
                command.Connection = this.Connection;
                await this.SqlResultsToStream(command, stream, defaultOutput);
            }
        }

        /// <summary>
        /// Executes SQL command and put results into stream.
        /// </summary>
        /// <param name="command">SQL command that will be executed.</param>
        /// <param name="stream">Output stream where results will be written.</param>
        /// <param name="defaultOutput">Default content that will be written into stream if there are no results.</param>
        /// <returns>Task</returns>
        public async Task Stream(DbCommand command, Stream stream, string defaultOutput = "")
        {
            command.Connection = this.Connection;
            await this.SqlResultsToStream(command, stream, defaultOutput);
        }

        private async Task SqlResultsToStream(DbCommand command, Stream stream, string defaultOutput)
        {
            try
            {
                await this.Mapper.ExecuteReader(command,
                    async reader =>
                    {
                        if (reader.HasRows)
                        {
                            var text = reader.GetString(0);
                            var buffеr = Encoding.UTF8.GetBytes(text);
                            await stream.WriteAsync(buffеr, 0, buffеr.Length).ConfigureAwait(false);
                            await stream.FlushAsync();
                        }
                        else
                        {
                            if (defaultOutput != "")
                                stream.Write(Encoding.UTF8.GetBytes(defaultOutput), 0, defaultOutput.Length);
                        }
                    });
            }
            catch (Exception ex)
            {
                if (this.ErrorHandler != null)
                    this.ErrorHandler(ex);
            }
            finally
            {
                command.Connection.Close();
            }
        }
    }
}