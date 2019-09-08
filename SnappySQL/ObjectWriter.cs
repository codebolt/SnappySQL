using SnappySql.Orm;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Reflection;
using System.Text;

namespace SnappySql
{
    internal interface IObjectDbWriter<T> where T : class
    {
        int WriteObject(SqlConnection conn, T obj);

        int WriteObjectList(SqlConnection conn, IEnumerable<T> list);
    }

    static internal class ObjectDbWriterHelpers
    {
        static internal int WriteObject<T>(SqlConnection conn, T obj, 
            string statement, IEnumerable<(PropertyInfo property, Column column)> columns,
            IValueToDBConverter converter)
        {
            using var cmd = new SqlCommand(statement, conn);
            foreach (var pc in columns)
            {
                object value = converter.ConvertValue(pc.property.GetMethod.Invoke(obj, null), pc.column.DbType);
                cmd.Parameters.AddWithValue(pc.property.Name, value);
            }
            return cmd.ExecuteNonQuery();
        }

        static internal int WriteObjects<T>(SqlConnection conn, IEnumerable<T> list, 
            string statement, IEnumerable<(PropertyInfo property, Column column)> columns,
            IValueToDBConverter converter)
        {
            int sum = 0;
            using var cmd = new SqlCommand(statement, conn);
            bool first = true;
            foreach (var obj in list)
            {
                foreach (var pc in columns)
                {
                    object value = converter.ConvertValue(pc.property.GetMethod.Invoke(obj, null), pc.column.DbType);
                    if (first)
                        cmd.Parameters.AddWithValue(pc.property.Name, value);
                    else
                        cmd.Parameters[pc.property.Name].Value = value;
                }
                sum += cmd.ExecuteNonQuery();
                first = false;
            }
            return sum;
        }
    }

    /// <summary>
    /// Used for classes with one or more keys (no identity columns) and one or more non-key columns.
    /// </summary>
    internal class StandardObjectDbWriter<T> : IObjectDbWriter<T> where T : class
    {
        private readonly string insertOrUpdateStmt;
        private readonly IValueToDBConverter valueConverter;
        private readonly IEnumerable<(PropertyInfo property, Column column)> columns;

        internal StandardObjectDbWriter(string insertOrUpdateStmt, IValueToDBConverter valueConverter, IEnumerable<(PropertyInfo, Column)> columns)
        {
            this.insertOrUpdateStmt = insertOrUpdateStmt;
            this.valueConverter = valueConverter;
            this.columns = columns;
        }

        int IObjectDbWriter<T>.WriteObject(SqlConnection conn, T obj) =>
            ObjectDbWriterHelpers.WriteObject(conn, obj, insertOrUpdateStmt, columns, valueConverter);

        int IObjectDbWriter<T>.WriteObjectList(SqlConnection conn, IEnumerable<T> list) =>
            ObjectDbWriterHelpers.WriteObject(conn, list, insertOrUpdateStmt, columns, valueConverter);
    }

    internal class IdentityObjectDbWriter<T> : IObjectDbWriter<T> where T : class
    {
        private readonly string insertStmt;
        private readonly string updateStmt;
        private readonly IValueToDBConverter valueToDBConverter;
        private readonly IEnumerable<(PropertyInfo property, Column column)> columns, nonIdentityColumns;
        private readonly (PropertyInfo property, Column column) identity;
        private readonly bool intIdentity;

        public IdentityObjectDbWriter(string insertStmt, string updateStmt,
            IValueToDBConverter valueToDBConverter,
            IEnumerable<(PropertyInfo property, Column column)> columns)
        {
            this.insertStmt = insertStmt;
            this.updateStmt = updateStmt;
            this.valueToDBConverter = valueToDBConverter;
            this.columns = columns;
            nonIdentityColumns = columns.Where(pc => !pc.column.Identity);
            identity = columns.Single(pc => pc.column.Identity);
            intIdentity = identity.property.PropertyType.IsAssignableFrom(typeof(int));
        }

        private bool IdentityMissing(T obj) =>
            Equals(identity.property.GetMethod.Invoke(obj, null), identity.column.IdentityDefault);

        int IObjectDbWriter<T>.WriteObject(SqlConnection conn, T obj)
        {
            if (!IdentityMissing(obj))
                return ObjectDbWriterHelpers.WriteObject(conn, obj, updateStmt, columns, valueToDBConverter);

            // When inserting identity-keyed objects, the insert statement
            // has an additional select identity_scope appended so the
            // new identity value is returned as a scalar
            using var cmd = new SqlCommand(insertStmt, conn);
            foreach (var pc in nonIdentityColumns)
            {
                object value = valueToDBConverter.ConvertValue(pc.property.GetMethod.Invoke(obj, null), pc.column.DbType);
                cmd.Parameters.AddWithValue(pc.property.Name, value);
            }
            object identityValue = cmd.ExecuteScalar();
            if (intIdentity && identityValue is decimal d)
                identityValue = decimal.ToInt32(d);

            identity.property.SetMethod.Invoke(obj, new[] { identityValue });
            return 1;
        }

        int IObjectDbWriter<T>.WriteObjectList(SqlConnection conn, IEnumerable<T> list)
        {
            var updList = list.Where(obj => !IdentityMissing(obj));
            int sum = 0;

            if (updList.Any())
                sum += ObjectDbWriterHelpers.WriteObjects(conn, updList, updateStmt,
                    columns, valueToDBConverter);

            var insList = list.Where(obj => IdentityMissing(obj));
            if (!insList.Any())
                return sum;

            using var cmd = new SqlCommand(insertStmt, conn);
            foreach(var obj in insList)
            {
                foreach (var pc in nonIdentityColumns)
                {
                    object value = valueToDBConverter.ConvertValue(pc.property.GetMethod.Invoke(obj, null), pc.column.DbType);
                    cmd.Parameters.AddWithValue(pc.property.Name, value);
                }
                object identityValue = cmd.ExecuteScalar();
                if (intIdentity && identityValue is decimal d)
                    identityValue = decimal.ToInt32(d);
                identity.property.SetMethod.Invoke(obj, new[] { identityValue });
                sum++;
            }
            return sum;
        }
    }

    internal static class ObjectWriterFactory
    {
        internal static IObjectDbWriter<T> CreateObjectDbWriter<T>(string tableName, IValueToDBConverter valueToDBConverter) where T : class
        {
            if (string.IsNullOrEmpty(tableName))
            {
                var table = (Table)Attribute.GetCustomAttribute(typeof(T), typeof(Table));
                tableName = table?.Name;
            }
            if (string.IsNullOrEmpty(tableName))
                throw new ArgumentException("Table name not defined for update/insert statement.");

            var columnProps = typeof(T).GetProperties()
                .Select(p => (prop: p, column: p.GetCustomAttribute<Column>()))
                .Where(pc => pc.column != null);
            var keyProps = columnProps.Where(pc => pc.column.Key);
            var identity = columnProps.Where(pc => pc.column.Identity);

            string MakeInsertStatement(bool identity = false, bool ifNotExists = false)
            {
                var insertColumns = columnProps.Where(pc => !pc.column.Identity);
                var sb = new StringBuilder();
                if (ifNotExists)
                    sb.Append($"if not exists (select top 1 1 from {tableName} where {MakeFilter()}) ");
                sb.Append($"insert into {tableName} (");
                sb.Append(insertColumns.Select(pc => pc.column.Name).Aggregate((col1, col2) => col1 + "," + col2));
                sb.Append(") values (");
                sb.Append(insertColumns.Select(pc => "@" + pc.prop.Name).Aggregate((prop1, prop2) => prop1 + "," + prop2));
                sb.Append(")");
                if (identity) sb.Append("; select scope_identity()");
                return sb.ToString();
            }

            string MakeFilter() => 
                keyProps.Select(pc => pc.column.Name + "=@" + pc.prop.Name)
                        .Aggregate((s1, s2) => s1 + " and " + s2);

            string MakeUpdateStatement()
            {
                var sb = new StringBuilder();
                sb.Append($"update {tableName} set ");
                sb.Append(columnProps.Where(pc => !pc.column.Key).Select(pc => pc.column.Name + "=@" + pc.prop.Name).Aggregate((s1, s2) => s1 + ", " + s2));
                sb.Append($" where {MakeFilter()}");
                return sb.ToString();
            }

            string MakeInsertOrUpdateStatement()
            {
                var sb = new StringBuilder();
                sb.Append(MakeUpdateStatement() + "; if @@rowcount=0 ");
                sb.Append($"if not exists (select top 1 from {tableName} where {MakeFilter()}) ");
                sb.Append(MakeInsertStatement());
                return sb.ToString();
            }

            if(identity.Any())
            {
                if (identity.Count() > 1 || 
                    columnProps.Any(pc => !pc.column.Identity && pc.column.Key))
                    throw new NotImplementedException("No support for models with multiple identity columns or mixed keys (identity and non-identity).");

                return new IdentityObjectDbWriter<T>(MakeInsertStatement(true),
                    MakeUpdateStatement(), valueToDBConverter, columnProps);
            }
            else
            {
                string statement;
                if (keyProps.Any() && keyProps.Count() < columnProps.Count())
                    statement = MakeInsertOrUpdateStatement();
                else
                    statement = MakeInsertStatement(ifNotExists: keyProps.Any());
                return new StandardObjectDbWriter<T>(statement, valueToDBConverter, columnProps);
            }
        }
    }
}
