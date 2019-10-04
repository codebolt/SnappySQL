using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;

namespace SnappySql
{
    /// <summary>
    /// Contains methods for extracting C# typed data from an SqlDataReader column.
    /// </summary>
    public class ValueFromDBConverter
    {
        protected sealed class ConverterMethod : Attribute { }

        static protected T SafeCast<T>(object obj) => obj == DBNull.Value ? default : (T)obj;

        [ConverterMethod]
        public virtual int GetInt(object value) => SafeCast<int>(value);

        [ConverterMethod]
        public virtual int? GetIntNullable(object value) => SafeCast<int?>(value);

        [ConverterMethod]
        public virtual string GetString(object value) => SafeCast<string>(value);

        private readonly IEnumerable<MethodInfo> converterMethods;

        public ValueFromDBConverter()
        {
            converterMethods = GetType().GetMethods()
                .Where(m => m.GetCustomAttribute(typeof(ConverterMethod)) != null);
        }

        internal MethodInfo GetConvertMethod(Type destType) =>
            converterMethods.Where(m => m.ReturnType.Equals(destType)).FirstOrDefault();
    }
}
