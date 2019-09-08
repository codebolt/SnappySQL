using SnappySql.Orm;
using System;
using System.Data;

namespace SnappySql
{
    public interface IValueToDBConverter
    {
        object ConvertValue(object value, SqlDbType dbType);
    }

    public class DefaultValueToDBConverter : IValueToDBConverter
    {
        public object ConvertValue(object value, SqlDbType dbType) => value ?? DBNull.Value;
    }
}
