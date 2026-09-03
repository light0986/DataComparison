using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;

namespace DataComparison.SQLserver
{
    /// <summary>
    /// 一段要在交易內執行的 SQL 陳述式,搭配其參數。
    /// </summary>
    public class SqlCommandText
    {
        public string CommandText { get; set; }
        public SqlParameter[] Parameters { get; set; }
    }

    public class SqlServerConnectionHelper : IDisposable
    {
        private readonly SqlConnection _connection;

        public SqlServerConnectionHelper(SqlServerConnectionInfo connectionInfo)
        {
            _connection = new SqlConnection(connectionInfo.BuildConnectionString());
        }

        public SqlServerConnectionHelper(string connectionString)
        {
            _connection = new SqlConnection(connectionString);
        }

        public void Open()
        {
            if (_connection.State != ConnectionState.Open)
            {
                _connection.Open();
            }
        }

        public void Close()
        {
            if (_connection.State != ConnectionState.Closed)
            {
                _connection.Close();
            }
        }

        public static bool TestConnection(SqlServerConnectionInfo connectionInfo, out string errorMessage)
        {
            errorMessage = null;
            try
            {
                using (var connection = new SqlConnection(connectionInfo.BuildConnectionString()))
                {
                    connection.Open();
                }
                return true;
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                return false;
            }
        }

        public DataTable ExecuteQuery(string sql, params SqlParameter[] parameters)
        {
            Open();
            using (var command = new SqlCommand(sql, _connection))
            {
                if (parameters != null && parameters.Length > 0)
                {
                    command.Parameters.AddRange(parameters);
                }

                using (var adapter = new SqlDataAdapter(command))
                {
                    var table = new DataTable();
                    adapter.Fill(table);
                    return table;
                }
            }
        }

        public int ExecuteNonQuery(string sql, params SqlParameter[] parameters)
        {
            Open();
            using (var command = new SqlCommand(sql, _connection))
            {
                if (parameters != null && parameters.Length > 0)
                {
                    command.Parameters.AddRange(parameters);
                }

                return command.ExecuteNonQuery();
            }
        }

        public object ExecuteScalar(string sql, params SqlParameter[] parameters)
        {
            Open();
            using (var command = new SqlCommand(sql, _connection))
            {
                if (parameters != null && parameters.Length > 0)
                {
                    command.Parameters.AddRange(parameters);
                }

                return command.ExecuteScalar();
            }
        }

        /// <summary>
        /// 依序執行多段 SQL 陳述式,全部包在同一個交易內;任何一段失敗就整個回復,不會留下做一半的資料。
        /// </summary>
        public void ExecuteInTransaction(IEnumerable<SqlCommandText> commands)
        {
            Open();
            using (var transaction = _connection.BeginTransaction())
            {
                try
                {
                    foreach (var commandText in commands)
                    {
                        using (var command = new SqlCommand(commandText.CommandText, _connection, transaction))
                        {
                            if (commandText.Parameters != null && commandText.Parameters.Length > 0)
                            {
                                command.Parameters.AddRange(commandText.Parameters);
                            }

                            command.ExecuteNonQuery();
                        }
                    }

                    transaction.Commit();
                }
                catch
                {
                    transaction.Rollback();
                    throw;
                }
            }
        }

        public void Dispose()
        {
            _connection.Dispose();
        }
    }
}
