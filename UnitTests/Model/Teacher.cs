using SnappySql.Orm;
using System;
using System.Data;

namespace UnitTests.Model
{
    [Table("Teacher")]
    public class Teacher : IEquatable<Teacher>
    {
        [Column("Id", SqlDbType.Int, Key = true, Identity = true)]
        public int Id { get; set; }

        [Column("Name", SqlDbType.NVarChar)]
        public string Name { get; set; }

        [Column("Address", SqlDbType.NVarChar)]
        public string Address { get; set; }

        [Column("Telephone", SqlDbType.NVarChar)]
        public string Telephone { get; set; }

        [Column("Email", SqlDbType.NVarChar)]
        public string Email { get; set; }

        // Used only in the unit tests
        public bool Equals(Teacher other) =>
            other != null &&
            Id == other.Id &&
            string.Equals(Name, other.Name) &&
            string.Equals(Address, other.Address) &&
            string.Equals(Telephone, other.Telephone) &&
            string.Equals(Email, other.Email);
    }
}
