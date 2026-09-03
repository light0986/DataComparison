using System.Data.SqlClient;

namespace DataComparison.SQLserver
{
    public class SqlServerConnectionInfo
    {
        public string ServerName { get; set; }
        public string DatabaseName { get; set; }
        public bool IntegratedSecurity { get; set; }
        public string UserId { get; set; }
        public string Password { get; set; }
        public int ConnectTimeoutSeconds { get; set; }

        public SqlServerConnectionInfo()
        {
            IntegratedSecurity = true;
            ConnectTimeoutSeconds = 15;
        }

        public string BuildConnectionString()
        {
            var builder = new SqlConnectionStringBuilder
            {
                DataSource = ServerName,
                InitialCatalog = DatabaseName,
                IntegratedSecurity = IntegratedSecurity,
                ConnectTimeout = ConnectTimeoutSeconds
            };

            if (!IntegratedSecurity)
            {
                builder.UserID = UserId;
                builder.Password = Password;
            }

            return builder.ConnectionString;
        }
    }
}
