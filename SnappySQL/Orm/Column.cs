using System;
using System.Data;

namespace SnappySql.Orm
{
    /// <summary>
    /// Maps a class property to a database table column.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public sealed class Column : Attribute
    {
        /// <summary>
        /// Name of the column as declared in the database.
        /// </summary>
        public string Name { get; set; }
        /// <summary>
        /// The data type of the column in the database.
        /// </summary>
        public SqlDbType DbType { get; set; }
        /// <summary>
        /// True iff this column is a key column in the database table.
        /// </summary>
        public bool Key { get; set; }
        /// <summary>
        /// True iff this key column is an identity column that is set by SQL Server.
        /// </summary>
        public bool Identity { get; set; }

        public object IdentityDefault { get; set; } = 0;

        public Column() { }

        public Column(string name) => Name = name;

        public Column(string name, SqlDbType dbType) : this(name) => DbType = dbType;
    }
}
