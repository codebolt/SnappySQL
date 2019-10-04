using SnappySql.Orm;
using System;
using System.Data;

namespace UnitTests.Model
{
    [Table("Class")]
    public class Class : IEquatable<Class>
    {
        [Column("Id", SqlDbType.NChar, Key = true)]
        public string Id { get; set; }

        [Column("Year", SqlDbType.Int, Key = true)]
        public int Year { get; set; }

        [Column("Term", SqlDbType.NChar, Key = true)]
        public Term Term { get; set; }

        [Column("Name", SqlDbType.NVarChar)]
        public string Name { get; set; }

        [Column("TeacherId", SqlDbType.Int)]
        public int TeacherId { get; set; }

        public bool Equals(Class other) =>
            other != null &&
            string.Equals(Id, other.Id) &&
            Year == other.Year &&
            Term == other.Term &&
            string.Equals(Name, other.Name) &&
            TeacherId == other.TeacherId;
    }
}
