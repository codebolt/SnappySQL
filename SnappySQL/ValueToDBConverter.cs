using System;
using System.Data;

namespace SnappySql
{
    public class ValueToDBConverter
    {
        virtual public object ConvertValue(object value, SqlDbType dbType) => 
            value ?? DBNull.Value;
    }
}
