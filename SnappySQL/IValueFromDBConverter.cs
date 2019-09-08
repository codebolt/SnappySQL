using SnappySql.Orm;
using System;
using System.Data.SqlClient;

namespace SnappySql
{
    /// <summary>
    /// Contains methods for extracting C# typed data from an SqlDataReader column.
    /// </summary>
    public interface IValueFromDBConverter
    {
        int GetInt(SqlDataReader reader, Column column);
        int? GetIntNullable(SqlDataReader reader, Column column);
        string GetString(SqlDataReader reader, Column column);
    }

    public class DefaultValueFromDBConverter : IValueFromDBConverter
    {
        public int? GetIntNullable(SqlDataReader reader, Column column)
        {
            int i = reader.GetOrdinal(column.Name);
            object value = reader.GetValue(i);
            return value == DBNull.Value ? null : (int?)value;
        }

        public int GetInt(SqlDataReader reader, Column column) => GetIntNullable(reader, column) ?? default;

        public string GetString(SqlDataReader reader, Column column)
        {
            int i = reader.GetOrdinal(column.Name);
            object value = reader.GetValue(i);
            return value == DBNull.Value ? null : (string)value;
        }
    }
}
