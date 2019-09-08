# SnappySQL
Lightweight C# SQL Server client wrapper which enables you to write data access code with minimal boilerplate and no performance penalty.

## Basic usage
Define your object-relational mappings using the Table and Column attributes on your class, like so:

    [Table("teacher")]
    public class Teacher : IEquatable<Teacher>
    {
        [Column("id", SqlDbType.Int, Key = true, Identity = true)]
        public int Id { get; set; }

        [Column("name", SqlDbType.NVarChar)]
        public string Name { get; set; }

        [Column("address", SqlDbType.NVarChar)
        public string Address { get; set; }

        [Column("telephone", SqlDbType.NVarChar)]
        public string Telephone { get; set; }

        [Column("email", SqlDbType.NVarChar)]
        public string Email { get; set; }
    };
    
Now you can use the SnappyEngine class to save (insert/update) and query, like so:

    var snappy = new SnappyEngine(connectionString);
    var myTeacher = new Teacher { Name = "John Doe", Address = "MyStreet 123", Telephone = "+12345678", Email = "email@test.com" } ;
    snappy.Save(myTeacher);
    // myTeacher should now be inserted, and myTeacher.Id is populated with the new identity

    var otherTeacher = snappy.Query<Teacher>("select * from teacher where name=@Name", ("Name", "Emma Stone"));
    // otherTeacher now holds first teacher found named Emma Stone
