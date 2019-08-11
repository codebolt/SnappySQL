using SnappySql.Orm;
using System;
using System.Data.SqlClient;

namespace SnappySql
{
    /// <summary>
    /// Contains methods for extracting C# typed data from an SqlDataReader column.
    /// </summary>
    public interface IValueExtractor
    {
        int GetInt(SqlDataReader reader, int i, Column column);
        int? GetIntNullable(SqlDataReader reader, int i, Column column);
        string GetString(SqlDataReader reader, int i, Column column);
    }

    public class DefaultValueExtractor : IValueExtractor
    {
        public int? GetIntNullable(SqlDataReader reader, int i, Column column)
        {
            object value = reader.GetValue(i);
            return value == DBNull.Value ? null : (int?)value;
        }

        public int GetInt(SqlDataReader reader, int i, Column column) => GetIntNullable(reader, i, column) ?? default;

        public string GetString(SqlDataReader reader, int i, Column column)
        {
            object value = reader.GetValue(i);
            return value == DBNull.Value ? null : (string)value;
        }
    }
}
