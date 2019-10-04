using SnappySql.Orm;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Reflection;
using System.Text;

#pragma warning disable CA2100 // Review SQL queries for security vulnerabilities

namespace SnappySql
{
    internal class ObjectWriter<T> where T : class
    {
        protected readonly string insertOrUpdateStmt;
        protected readonly string deleteStmt;
        protected readonly ValueToDBConverter valueConverter;
        protected readonly IEnumerable<(PropertyInfo property, Column column)> columns, keyColumns;

        internal ObjectWriter(string insertOrUpdateStmt, string deleteStmt, ValueToDBConverter valueConverter, IEnumerable<(PropertyInfo, Column)> columns)
        {
            this.insertOrUpdateStmt = insertOrUpdateStmt;
            this.deleteStmt = deleteStmt;
            this.valueConverter = valueConverter;
            this.columns = columns;
            keyColumns = this.columns.Where(pc => pc.column.Key);
        }

        protected int ExecuteNonQuery(SqlConnection conn, T obj,
            string statement, IEnumerable<(PropertyInfo property, Column column)> columns)
        {
            using var cmd = new SqlCommand(statement, conn);
            foreach (var pc in columns)
            {
                object value = valueConverter.ConvertValue(pc.property.GetMethod.Invoke(obj, null), pc.column.DbType);
                cmd.Parameters.AddWithValue(pc.property.Name, value);
            }
            return cmd.ExecuteNonQuery();
        }

        protected int ExecuteNonQueryList(SqlConnection conn, IEnumerable<T> list,
            string statement, IEnumerable<(PropertyInfo property, Column column)> columns)
        {
            int sum = 0;
            using var cmd = new SqlCommand(statement, conn);
            // Reuse the same SqlCommand for each object in the list
            bool first = true;
            foreach (var obj in list)
            {
                foreach (var pc in columns)
                {
                    object value = valueConverter.ConvertValue(pc.property.GetMethod.Invoke(obj, null), pc.column.DbType);
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

        internal virtual int WriteObject(SqlConnection conn, T obj) =>
            ExecuteNonQuery(conn, obj, insertOrUpdateStmt, columns);

        internal virtual int WriteObjectList(SqlConnection conn, IEnumerable<T> list) =>
            ExecuteNonQueryList(conn, list, insertOrUpdateStmt, columns);

        internal virtual int DeleteObject(SqlConnection conn, T obj) =>
            ExecuteNonQuery(conn, obj, deleteStmt, keyColumns);
    }

    internal class IdentityObjectWriter<T> : ObjectWriter<T> where T : class
    {
        private readonly string insertStmt;
        private readonly string updateStmt;
        private readonly IEnumerable<(PropertyInfo property, Column column)> nonIdentityColumns;
        private readonly (PropertyInfo property, Column column) identity;
        private readonly bool intIdentity;

        public IdentityObjectWriter(string insertStmt, string updateStmt, string deleteStmt,
            ValueToDBConverter valueConverter, IEnumerable<(PropertyInfo, Column)> columns) 
            : base("", deleteStmt, valueConverter, columns)
        {
            this.insertStmt = insertStmt;
            this.updateStmt = updateStmt;
            nonIdentityColumns = this.columns.Where(pc => !pc.column.Identity);
            identity = this.columns.Single(pc => pc.column.Identity);
            intIdentity =  new[] { typeof(long), typeof(ulong), typeof(long?), typeof(ulong?) }
                .Any(t => TypeDescriptor.GetConverter(identity.property.PropertyType).CanConvertTo(t));
        }

        private bool IdentityMissing(T obj) =>
            Equals(identity.property.GetMethod.Invoke(obj, null), identity.column.IdentityDefault);

        internal override int WriteObject(SqlConnection conn, T obj)
        {
            if (!IdentityMissing(obj)) // If identity-property is set, execute update
                return ExecuteNonQuery(conn, obj, updateStmt, columns);

            // When inserting identity-keyed objects, the insert statement
            // has an additional select identity_scope appended so the
            // new identity value is returned as a scalar
            using var cmd = new SqlCommand(insertStmt, conn);
            foreach (var pc in nonIdentityColumns)
            {
                object value = valueConverter.ConvertValue(pc.property.GetMethod.Invoke(obj, null), pc.column.DbType);
                cmd.Parameters.AddWithValue(pc.property.Name, value);
            }
            object identityValue = cmd.ExecuteScalar();
            if (intIdentity && identityValue is decimal d)
                identityValue = decimal.ToInt32(d);

            identity.property.SetMethod.Invoke(obj, new[] { identityValue });
            return 1;
        }

        internal override int WriteObjectList(SqlConnection conn, IEnumerable<T> list)
        {
            // Separate the list into updates and inserts
            var updList = list.Where(obj => !IdentityMissing(obj));
            int sum = 0;

            if (updList.Any())
                sum += ExecuteNonQueryList(conn, updList, updateStmt, columns);

            var insList = list.Where(obj => IdentityMissing(obj));
            if (!insList.Any())
                return sum;

            using var cmd = new SqlCommand(insertStmt, conn);
            bool first = true;
            foreach(var obj in insList)
            {
                foreach (var pc in nonIdentityColumns)
                {
                    object value = valueConverter.ConvertValue(pc.property.GetMethod.Invoke(obj, null), pc.column.DbType);
                    if (first)
                        cmd.Parameters.AddWithValue(pc.property.Name, value);
                    else
                        cmd.Parameters[pc.property.Name].Value = value;
                }
                object identityValue = cmd.ExecuteScalar();
                if (intIdentity && identityValue is decimal d)
                    identityValue = decimal.ToInt32(d);
                identity.property.SetMethod.Invoke(obj, new[] { identityValue });
                sum++;
                first = false;
            }
            return sum;
        }
    }

    internal static class ObjectWriterFactory
    {
        internal static ObjectWriter<T> CreateObjectDbWriter<T>(string tableName, ValueToDBConverter valueToDBConverter) where T : class
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
                sb.Append(MakeInsertStatement());
                return sb.ToString();
            }

            string deleteStatement = $"delete from {tableName} where {MakeFilter()}";

            if (identity.Any())
            {
                if (identity.Count() > 1 || 
                    columnProps.Any(pc => !pc.column.Identity && pc.column.Key))
                    throw new NotImplementedException("No support for models with multiple identity columns or mixed keys (identity and non-identity).");

                return new IdentityObjectWriter<T>(MakeInsertStatement(true),
                    MakeUpdateStatement(), deleteStatement, valueToDBConverter, columnProps);
            }
            else
            {
                string statement;
                if (keyProps.Any() && keyProps.Count() < columnProps.Count())
                    statement = MakeInsertOrUpdateStatement();
                else
                    statement = MakeInsertStatement(ifNotExists: keyProps.Any());
                return new ObjectWriter<T>(statement, deleteStatement, valueToDBConverter, columnProps);
            }
        }
    }
}
