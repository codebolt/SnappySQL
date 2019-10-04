using SnappySql.Orm;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Reflection.Emit;

namespace SnappySql
{
    internal static class ObjectReaderFactory
    {
        private static Func<ValueFromDBConverter, SqlDataReader, T> GenReader<T>(ValueFromDBConverter converter)
        {
            var dataReaderGetOrdinal = typeof(SqlDataReader).GetMethod(nameof(SqlDataReader.GetOrdinal), new[] { typeof(string) });
            var dataReaderGetValue = typeof(SqlDataReader).GetMethod(nameof(SqlDataReader.GetValue), new[] { typeof(int) });
            var ctor = typeof(T).GetConstructor(new Type[0]) ?? throw new ArgumentException("Type missing default constructor.");
            var properties = typeof(T).GetProperties()
                .Where(p => Attribute.IsDefined(p, typeof(Column)));
            var dm = new DynamicMethod("Read" + typeof(T).Name, typeof(T), 
                new[] { typeof(ValueFromDBConverter), typeof(SqlDataReader) });

            var gen = dm.GetILGenerator();
            gen.DeclareLocal(typeof(T));
            gen.DeclareLocal(typeof(int));
            gen.DeclareLocal(typeof(object));
            gen.Emit(OpCodes.Newobj, ctor); // push obj
            gen.Emit(OpCodes.Stloc_0);
            
            foreach(var property in properties)
            {
                var column = (Column)Attribute.GetCustomAttribute(property, typeof(Column));
                var convMethod = converter.GetConvertMethod(property.PropertyType) 
                    ?? throw new ArgumentException("Unable to find converter for property.");
                var setter = property.GetSetMethod() 
                    ?? throw new ArgumentException("Property missing setter.");
                gen.Emit(OpCodes.Ldarg_1); // SqlDataReader
                gen.Emit(OpCodes.Ldstr, column.Name);
                gen.Emit(OpCodes.Callvirt, dataReaderGetOrdinal); // dataReader.GetOrdinal
                gen.Emit(OpCodes.Stloc_1);

                gen.Emit(OpCodes.Ldarg_1); // SqlDataReader
                gen.Emit(OpCodes.Ldloc_1);
                gen.Emit(OpCodes.Callvirt, dataReaderGetValue); // dataReader.GetValue
                gen.Emit(OpCodes.Stloc_2);

                gen.Emit(OpCodes.Ldloc_0); // obj
                gen.Emit(OpCodes.Ldarg_0); // ValueFromDBConverter
                gen.Emit(OpCodes.Ldloc_2);
                //gen.Emit(OpCodes.Ldc_I4, (int)column.DbType);
                gen.Emit(OpCodes.Callvirt, convMethod); // converter.GetXxx(object value, int type)
                gen.Emit(OpCodes.Call, setter); // obj.Property = 
                // todo: cache ordinals for optimization
                // todo: gracefully handle missing properties
            }
            gen.Emit(OpCodes.Ldloc_0);
            gen.Emit(OpCodes.Ret);

            return (Func<ValueFromDBConverter, SqlDataReader, T>) dm.CreateDelegate(typeof(Func<ValueFromDBConverter, SqlDataReader, T>));
        }

        internal static ObjectReader<T> CreateObjectReader<T>(ValueFromDBConverter valueFromDBConverter) where T : class
        {
            var reader = GenReader<T>(valueFromDBConverter);
            return new ObjectReader<T>(reader, valueFromDBConverter);
        }
    }

    internal class ObjectReader<T> where T : class
    {
        private readonly Func<ValueFromDBConverter, SqlDataReader, T> reader;
        private readonly ValueFromDBConverter valueFromDBConverter;

        internal ObjectReader(Func<ValueFromDBConverter, SqlDataReader, T> reader,
            ValueFromDBConverter valueFromDBConverter)
        {
            this.reader = reader;
            this.valueFromDBConverter = valueFromDBConverter;
        }

        internal IList<T> ReadAll(SqlDataReader reader)
        {
            var list = new List<T>();
            T obj;
            while (null != (obj = Read(reader)))
                list.Add(obj);
            return list;
        }

        internal T Read(SqlDataReader dataReader) =>
            dataReader.Read() ? reader.Invoke(valueFromDBConverter, dataReader) : null;
    }
}
