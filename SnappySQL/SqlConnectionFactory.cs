using System.Data.SqlClient;

namespace SnappySql
{
    public interface ISqlConnectionFactory
    {
        SqlConnection GetConnection();
    }

    public class SqlConnectionFactory : ISqlConnectionFactory
    {
        private readonly string connectionString;

        public SqlConnectionFactory(string connectionString) => this.connectionString = connectionString;

        public SqlConnection GetConnection() => new SqlConnection(connectionString);
    }
}
