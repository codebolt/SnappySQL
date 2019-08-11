using SnappySql.Orm;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace SnappySql
{
    /// <summary>
    /// This class generates functions that read a typed object from a database result set,
    /// </summary>
    public class ReaderGenerator
    {
        private readonly IValueExtractor valueExtractor;
        private readonly MethodInfo getIntMethod;
        private readonly MethodInfo getIntNullableMethod;
        private readonly MethodInfo getStringMethod;

        private MethodInfo GetExtractor(string methodName) =>
            valueExtractor.GetType().GetMethod(methodName, BindingFlags.Public)
                ?? throw new KeyNotFoundException("ValueExtractor method " + methodName + " not found");

        public ReaderGenerator() : this(new DefaultValueExtractor()) { }

        public ReaderGenerator(IValueExtractor valueExtractor)
        {
            this.valueExtractor = valueExtractor;
            getIntMethod = GetExtractor("GetInt");
            getIntNullableMethod = GetExtractor("GetIntNullable");
            getStringMethod = GetExtractor("GetString");
        }

        private MethodInfo GetExtractorForProperty(PropertyInfo property)
        {
            if (typeof(string).IsAssignableFrom(property.PropertyType))
                return getStringMethod;
            else if (typeof(int).IsAssignableFrom(property.PropertyType))
                return getIntMethod;
            else if (typeof(int?).IsAssignableFrom(property.PropertyType))
                return getIntNullableMethod;
            else
                throw new ArgumentException("Unsupported Column property type " + property.PropertyType.Name + " in class " + property.DeclaringType.Name + ".");
        }

        public Func<SqlDataReader, IValueExtractor, T> GenerateReader<T>()
        {
            var tableAttr = (Table)Attribute.GetCustomAttribute(typeof(T), typeof(Table));
            var properties = typeof(T).GetProperties().Where(p => Attribute.IsDefined(p, typeof(Column)));

            var dm = new DynamicMethod("Read" + typeof(T).Name, typeof(T), new[] { typeof(SqlDataReader), typeof(IValueExtractor) });
            var gen = dm.GetILGenerator();

            // var obj = new MyType();
            gen.DeclareLocal(typeof(T));
            gen.Emit(OpCodes.Newobj, typeof(T).GetConstructor(new Type[0]));
            gen.Emit(OpCodes.Stloc_0);
            foreach (var property in properties)
            {
                var colAttr = (Column)Attribute.GetCustomAttribute(property, typeof(Column));
                var getter = GetExtractorForProperty(property);

                // obj.Property = GetXxx(reader, columnName)
                gen.Emit(OpCodes.Ldloc_0); // push obj
                gen.Emit(OpCodes.Ldarg_0); // push SqlDataReader as parameter to GetXxx
                gen.Emit(OpCodes.Ldstr, colAttr.Name); // push column name as parameter to GetXxx
                gen.Emit(OpCodes.Ldarg_1); // push ValueExtractor
                gen.Emit(OpCodes.Call, getter); // invoke valueExtractor.GetXxx
                gen.Emit(OpCodes.Call, property.GetSetMethod()); // pass the result of GetXxx to obj.Property setter method
            }
            // return obj;
            gen.Emit(OpCodes.Ldloc_0); // push obj
            gen.Emit(OpCodes.Ret); // return
            return (Func<SqlDataReader, IValueExtractor, T>)dm.CreateDelegate(typeof(Func<SqlDataReader, IValueExtractor, T>));
        }
    }
}
