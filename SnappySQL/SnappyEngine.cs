using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;

namespace SnappySql
{
    public class SnappyEngine
    {
        public ISqlConnectionFactory ConnectionFactory { get; }

        private readonly IValueFromDBConverter valueFromDBConverter;
        private readonly IValueToDBConverter valueToDBConverter;

        public SnappyEngine(ISqlConnectionFactory connectionFactory, IValueFromDBConverter valueFromDBConverter = null, IValueToDBConverter valueToDBConverter = null)
        {
            this.valueFromDBConverter = valueFromDBConverter ?? new DefaultValueFromDBConverter();
            this.valueToDBConverter = valueToDBConverter ?? new DefaultValueToDBConverter();
            ConnectionFactory = connectionFactory;
        }

        public SnappyEngine(string connectionString, IValueFromDBConverter valueFromDBConverter = null, IValueToDBConverter valueToDBConverter = null)
        {
            this.valueFromDBConverter = valueFromDBConverter ?? new DefaultValueFromDBConverter();
            this.valueToDBConverter = valueToDBConverter ?? new DefaultValueToDBConverter();
            ConnectionFactory = new SqlConnectionFactory(connectionString);
        }

        #region Query

        #region Cache ObjectReader<T> instances for reuse
        private readonly Dictionary<Type, object> _readerCache = new Dictionary<Type, object>();
        private ObjectReader<T> GetObjectReader<T>() where T : class
        {
            ObjectReader<T> reader;
            lock(_readerCache)
            {
                var type = typeof(T);
                if (_readerCache.TryGetValue(type, out var readerObj))
                    reader = (ObjectReader<T>)readerObj;
                else
                {
                    reader = ObjectReaderFactory.CreateObjectReader<T>(valueFromDBConverter);
                    _readerCache[type] = reader; 
                }
            }
            return reader;
        }
        #endregion

        #region QueryList
        public IList<T> QueryList<T>(SqlConnection conn, string query) where T : class
        {
            using var cmd = new SqlCommand(query, conn);
            using var dataReader = cmd.ExecuteReader();
            return GetObjectReader<T>().ReadAll(dataReader);
        }

        public IList<T> QueryList<T>(SqlConnection conn, string query, params (string, object)[] parameters) where T : class
        {
            using var cmd = new SqlCommand(query, conn);
            foreach (var (name, value) in parameters)
                cmd.Parameters.AddWithValue(name, value ?? DBNull.Value);
            using var dataReader = cmd.ExecuteReader();
            return GetObjectReader<T>().ReadAll(dataReader);
        }

        public IList<T> QueryList<T>(SqlConnection conn, string query, params (string, object, SqlDbType)[] parameters) where T : class
        {
            using var cmd = new SqlCommand(query, conn);
            foreach (var (name, value, dbType) in parameters)
                cmd.Parameters.AddWithValue(name, valueToDBConverter.ConvertValue(value, dbType));
            using var dataReader = cmd.ExecuteReader();
            return GetObjectReader<T>().ReadAll(dataReader);
        }

        public IList<T> QueryList<T>(string query) where T : class
        {
            using var conn = ConnectionFactory.GetConnection();
            conn.Open();
            return QueryList<T>(conn, query);
        }

        public IList<T> QueryList<T>(string query, params (string, object)[] parameters) where T : class
        {
            using var conn = ConnectionFactory.GetConnection();
            conn.Open();
            return QueryList<T>(conn, query, parameters);
        }

        public IList<T> QueryList<T>(string query, params (string, object, SqlDbType)[] parameters) where T : class
        {
            using var conn = ConnectionFactory.GetConnection();
            conn.Open();
            return QueryList<T>(conn, query, parameters);
        }
        #endregion

        #region Query
        public T Query<T>(SqlConnection conn, string query, params (string, object)[] parameters) where T : class
        {
            using var cmd = new SqlCommand(query, conn);
            foreach (var (name, value) in parameters)
                cmd.Parameters.AddWithValue(name, value ?? DBNull.Value);
            using var dataReader = cmd.ExecuteReader();
            return GetObjectReader<T>().Read(dataReader);
        }

        public T Query<T>(string query, params (string, object)[] parameters) where T : class
        {
            using var conn = ConnectionFactory.GetConnection();
            conn.Open();
            return Query<T>(conn, query, parameters);
        }

        public T Query<T>(SqlConnection conn, string query, params (string, object, SqlDbType)[] parameters) where T : class
        {
            using var cmd = new SqlCommand(query, conn);
            foreach (var (name, value, dbType) in parameters)
                cmd.Parameters.AddWithValue(name, valueToDBConverter.ConvertValue(value, dbType));
            using var dataReader = cmd.ExecuteReader();
            return GetObjectReader<T>().Read(dataReader);
        }

        public T Query<T>(string query, params (string, object, SqlDbType)[] parameters) where T : class
        {
            using var conn = ConnectionFactory.GetConnection();
            conn.Open();
            return Query<T>(conn, query, parameters);
        }
        #endregion

        #region QueryScalar
        public object QueryScalar(SqlConnection conn, string query, params (string, object)[] parameters)
        {
            using var cmd = new SqlCommand(query, conn);
            foreach (var (name, value) in parameters)
                cmd.Parameters.AddWithValue(name, value ?? DBNull.Value);
            return cmd.ExecuteScalar();
        }

        public object QueryScalar(string query, params (string, object)[] parameters)
        {
            using var conn = ConnectionFactory.GetConnection();
            conn.Open();
            return QueryScalar(conn, query, parameters);
        }

        public object QueryScalar(SqlConnection conn, string query, params (string, object, SqlDbType)[] parameters)
        {
            using var cmd = new SqlCommand(query, conn);
            foreach (var (name, value, dbType) in parameters)
                cmd.Parameters.AddWithValue(name, valueToDBConverter.ConvertValue(value, dbType));
            return cmd.ExecuteScalar();
        }

        public object QueryScalar(string query, params (string, object, SqlDbType)[] parameters)
        {
            using var conn = ConnectionFactory.GetConnection();
            conn.Open();
            return QueryScalar(conn, query, parameters);
        }

        public T QueryScalar<T>(SqlConnection conn, string query, params (string, object)[] parameters) where T : struct =>
            (T) QueryScalar(conn, query, parameters);

        public T QueryScalar<T>(string query, params (string, object)[] parameters) where T : struct =>
            (T) QueryScalar(query, parameters);

        public T QueryScalar<T>(SqlConnection conn, string query, params (string, object, SqlDbType)[] parameters) =>
            (T) QueryScalar(conn, query, parameters);

        public T QueryScalar<T>(string query, params (string, object, SqlDbType)[] parameters) =>
            (T) QueryScalar(query, parameters);
        #endregion

        #region QueryScalarList
        public IList<T> QueryScalarList<T>(SqlConnection conn, string query, params (string, object)[] parameters)
        {
            using var cmd = new SqlCommand(query, conn);
            foreach (var (name, value) in parameters)
                cmd.Parameters.AddWithValue(name, value ?? DBNull.Value);
            using var reader = cmd.ExecuteReader();
            var list = new List<T>();
            while (reader.NextResult())
                list.Add((T) reader.GetValue(0));
            return list;
        }

        public IList<T> QueryScalarList<T>(string query, params (string, object)[] parameters)
        {
            using var conn = ConnectionFactory.GetConnection();
            conn.Open();
            return QueryScalarList<T>(conn, query, parameters);
        }

        public IList<T> QueryScalarList<T>(SqlConnection conn, string query, params (string, object, SqlDbType)[] parameters)
        {
            using var cmd = new SqlCommand(query, conn);
            foreach (var (name, value, dbType) in parameters)
                cmd.Parameters.AddWithValue(name, valueToDBConverter.ConvertValue(value, dbType));
            using var reader = cmd.ExecuteReader();
            var list = new List<T>();
            while (reader.NextResult())
                list.Add((T)reader.GetValue(0));
            return list;
        }

        public IList<T> QueryScalarList<T>(string query, params (string, object, SqlDbType)[] parameters)
        {
            using var conn = ConnectionFactory.GetConnection();
            conn.Open();
            return QueryScalarList<T>(conn, query, parameters);
        }
        #endregion

        #endregion

        #region Save (update/insert)

        #region ObjectWriter cache
        private readonly Dictionary<string, object> _objectWriterCache = new Dictionary<string, object>();
        private IObjectDbWriter<T> GetObjectWriter<T>(string tableName = "") where T : class
        {
            string key = typeof(T).FullName;
            if (!string.IsNullOrEmpty(tableName))
                key += "#" + tableName;

            IObjectDbWriter<T> ow;
            lock(_objectWriterCache)
            {
                if (_objectWriterCache.TryGetValue(key, out var value))
                    ow = (IObjectDbWriter<T>) value;
                else
                {
                    ow = ObjectWriterFactory.CreateObjectDbWriter<T>(tableName, valueToDBConverter);
                    _objectWriterCache.Add(key, ow);
                }
            }
            return ow;
        }
        #endregion

        public int Save<T>(SqlConnection conn, T obj, string tableName = "") where T : class 
            => GetObjectWriter<T>(tableName).WriteObject(conn, obj);

        public int Save<T>(T obj, string tableName = "") where T : class
        {
            using var conn = ConnectionFactory.GetConnection();
            conn.Open();
            return Save(conn, obj, tableName);
        }

        public int SaveList<T>(SqlConnection conn, IEnumerable<T> list, string tableName = "") where T : class
            => GetObjectWriter<T>(tableName).WriteObjectList(conn, list);

        public int SaveList<T>(IEnumerable<T> list, string tableName = "") where T : class
        {
            using var conn = ConnectionFactory.GetConnection();
            conn.Open();
            return SaveList(conn, list, tableName);
        }
        #endregion
    }
}
