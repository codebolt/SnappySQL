using SnappySql.Orm;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace SnappySql
{
    internal static class ObjectReaderFactory
    {
        private static readonly Type[] valueFromDBParameterTypes = 
            { typeof(SqlDataReader), typeof(Column) };

        private static MethodInfo GetValueConverterForProperty(PropertyInfo property, IValueFromDBConverter valueFromDBConverter)
        {
            MethodInfo GetValueFromDBMethod(string methodName) =>
                valueFromDBConverter.GetType().GetMethod(methodName, valueFromDBParameterTypes);

            MethodInfo method = null;
            if (typeof(string).IsAssignableFrom(property.PropertyType))
                method = GetValueFromDBMethod(nameof(valueFromDBConverter.GetString));
            else if (typeof(int).IsAssignableFrom(property.PropertyType))
                method = GetValueFromDBMethod(nameof(valueFromDBConverter.GetInt));
            else if (typeof(int?).IsAssignableFrom(property.PropertyType))
                method = GetValueFromDBMethod(nameof(valueFromDBConverter.GetIntNullable));

            return method ??
                throw new ArgumentException("Unsupported Column property type " + property.PropertyType.Name + " in class " + property.DeclaringType.FullName + ".");
        }

        private static Action<T, IValueFromDBConverter, Column, SqlDataReader> GenPropSetter<T>(MethodInfo setter, MethodInfo conv)
        {
            var dm = new DynamicMethod("SetProp_" + new Guid().ToString(), null,
                new[] { typeof(T), typeof(IValueFromDBConverter), typeof(Column), typeof(SqlDataReader) });
            var gen = dm.GetILGenerator();

            gen.Emit(OpCodes.Ldarg_0); // push obj

            gen.Emit(OpCodes.Ldarg_1); // push ValueExtractor
            gen.Emit(OpCodes.Ldarg_3); // push SqlDataReader as parameter to GetXxx
            gen.Emit(OpCodes.Ldarg_2); // push Column
            gen.Emit(OpCodes.Call, conv); // invoke valueExtractor.GetXxx

            gen.Emit(OpCodes.Call, setter); // pass the result of GetXxx to obj.Property setter method
            gen.Emit(OpCodes.Ret);

            return (Action<T, IValueFromDBConverter, Column, SqlDataReader>)
                dm.CreateDelegate(typeof(Action<T, IValueFromDBConverter, Column, SqlDataReader>));
        }

        internal static ObjectReader<T> CreateObjectReader<T>(IValueFromDBConverter valueFromDBConverter) where T : class
        {
            var properties = typeof(T).GetProperties()
                .Where(p => Attribute.IsDefined(p, typeof(Column)))
                .Select(prop => (set: prop.GetSetMethod(),
                    col: (Column)Attribute.GetCustomAttribute(prop, typeof(Column)),
                    conv: GetValueConverterForProperty(prop, valueFromDBConverter)))
                .Select(prop => (prop.col,
                    pset: GenPropSetter<T>(prop.set, prop.conv)));
            return new ObjectReader<T>(properties, valueFromDBConverter);
        }
    }

    internal class ObjectReader<T> where T : class
    {
        private readonly IEnumerable<(Column column, Action<T, IValueFromDBConverter, Column, SqlDataReader> propSetter)> columnSetters;
        private readonly ConstructorInfo ctor;
        private readonly IValueFromDBConverter valueFromDBConverter;

        internal ObjectReader(IEnumerable<(Column column, Action<T, IValueFromDBConverter, Column, SqlDataReader> propSetter)> columnSetters,
            IValueFromDBConverter valueFromDBConverter)
        {
            this.columnSetters = columnSetters;
            this.valueFromDBConverter = valueFromDBConverter;
            ctor = typeof(T).GetConstructor(new Type[0]);
        }

        internal IList<T> ReadAll(SqlDataReader reader)
        {
            var list = new List<T>();
            T obj;
            while (null != (obj = Read(reader)))
                list.Add(obj);
            return list;
        }

        internal T Read(SqlDataReader reader)
        {
            T obj = null;
            if(reader.Read())
            {
                obj = (T) ctor.Invoke(null);
                foreach(var cs in columnSetters)
                    cs.propSetter.Invoke(obj, valueFromDBConverter, cs.column, reader);
            }
            return obj;
        }
    }
}
