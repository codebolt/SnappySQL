using SnappySql.Orm;
using System;
using System.Data;

namespace UnitTests.Model
{
    [Table("Student")]
    class Student : IEquatable<Student>
    {
        [Column("Id", DbType = SqlDbType.Int, Key = true, Identity = true)]
        public int Id { get; set; }

        [Column("Name", DbType = SqlDbType.NVarChar)]
        public string Name { get; set; }

        [Column("Gender", DbType = SqlDbType.Char)]
        public Gender Gender { get; set; }

        [Column("Birthday", DbType = SqlDbType.Date)]
        public int Birthday { get; set; }

        public bool Equals(Student other) =>
            other != null &&
            Id == other.Id &&
            string.Equals(Name, other.Name) &&
            Gender == other.Gender &&
            Birthday == other.Birthday;
    }
}
