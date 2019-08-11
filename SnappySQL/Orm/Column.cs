using System;
using System.Data;

namespace SnappySql.Orm
{
    /// <summary>
    /// Maps a class property to a database table column.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class Column : Attribute
    {
        /// <summary>
        /// Name of the column as declared in the database.
        /// </summary>
        public string Name { get; set; }
        /// <summary>
        /// The data type of the column in the database.
        /// </summary>
        public SqlDbType DbType { get; set; }
    }
}
