using Microsoft.SqlServer.Dac;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SnappySql;
using SnappySql.Orm;
using System;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using UnitTests.Model;

namespace UnitTests
{
    [TestClass]
    public class UnitTests
    {
        const string testDbName = "SnappySqlUnitTestDB";
        const string connectionString = "Data Source=localhost; Integrated Security=SSPI; Initial Catalog=SnappySqlUnitTestDB;";

        #region Custom value conversions from/to DB for our tests
        /*
         * The model used for our tests has some C# enums that we want to store as single
         * char's in the database. To accomplish this we need some custom logic to translate
         * the enums to and from their database represantations. We also want to trim all
         * strings read from the database, and we want to represent date's as int's in C#.
         */
        class CustomValueFromDBConverter : ValueFromDBConverter
        {
            [ConverterMethod]
            public override string GetString(object obj) =>
                base.GetString(obj)?.Trim();

            [ConverterMethod]
            public override int GetInt(object obj)
            {
                if (obj is DateTime dt)
                    return dt.Year * 10000 + dt.Month * 100 + dt.Day;
                else
                    return base.GetInt(obj);
            }

            static T GetEnumValue<T>((T e, string s)[] dbValues, object str) where T : struct =>
                dbValues.Where(v => string.Equals(v.s, (string)str)).Select(v => v.e).Single();

            [ConverterMethod]
            public Gender GetGender(object str) => GetEnumValue(EnumValues.GenderValues, str);

            [ConverterMethod]
            public Term GetTerm(object str) => GetEnumValue(EnumValues.TermValues, str);
        }
        class CustomValueToDBConverter : ValueToDBConverter
        {
            public override object ConvertValue(object value, SqlDbType dbType)
            {
                static string GetEnumString<T>((T e, string s)[] dbValues, T e) =>
                    dbValues.Where(v => v.e.Equals(e)).Select(v => v.s).Single();

                if (dbType.IsStringType() && !(value is string))
                {
                    if (value is Gender g)
                        return GetEnumString(EnumValues.GenderValues, g);
                    if (value is Term t)
                        return GetEnumString(EnumValues.TermValues, t);
                }

                if (dbType.IsDateType() && value is int n)
                    return new DateTime(n / 10000, n / 100 % 100, n % 100);

                return base.ConvertValue(value, dbType);
            }
        }
        #endregion

        private readonly SnappyEngine snappy = new SnappyEngine(connectionString, new CustomValueFromDBConverter(), new CustomValueToDBConverter());

        [TestInitialize]
        public void InitTestDB()
        {
            var dacServices = new DacServices(connectionString);
            string dacPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestDB.dacpac");
            var dacPackage = DacPackage.Load(dacPath);
            dacServices.Deploy(dacPackage, testDbName, upgradeExisting: true);
        }

        [TestCleanup]
        public void DeleteTestDB()
        {
            using var conn = snappy.ConnectionFactory.GetConnection();
            try
            {
                conn.Open();
                {
                    using var cmd = new SqlCommand("truncate table Teacher; truncate table Student", conn);
                    cmd.ExecuteNonQuery();
                }
                conn.ChangeDatabase("master");
                {
                    using var cmd = new SqlCommand("drop database SnappySqlUnitTestDB", conn);
                    cmd.ExecuteNonQuery();
                }
            }
            catch(SqlException e) 
            {
                Console.Out.WriteLine("Exception cleaning up database: " + e.ToString());
            }
        }

        [TestMethod]
        public void CompositeTest()
        {
            using var conn = snappy.ConnectionFactory.GetConnection();
            conn.Open();
            var teachers = new[] {
                new Teacher
                {
                    Name = "John Doe",
                    Address = "Moses Rd 12",
                    Telephone = "99991111",
                    Email = "noreply@test.com",
                },
                new Teacher
                {
                    Name = "Ella Johnson",
                    Email = "something@test.com"
                },
                new Teacher
                {
                    Name = "Strangename Horsemaster",
                    Email = "snamehor@school.com"
                }
            };
            int res = snappy.SaveList(conn, teachers);
            Assert.AreEqual(res, teachers.Length);
            Assert.IsFalse(teachers.Any(t => t.Id <= 0));
            var teachers2 = snappy.QueryList<Teacher>("select * from Teacher");
            Assert.IsTrue(teachers2.Any(t => t.Equals(teachers[0])));

            var classes = new[]
            {
                new Class
                {
                    Id = "ENG101",
                    Name = "English for beginners",
                    Year = 2019,
                    Term = Term.Fall,
                    TeacherId = teachers[0].Id
                },
                new Class
                {
                    Id = "MAT443",
                    Name = "Calculus for statisticians",
                    Year = 2019,
                    Term = Term.Fall,
                    TeacherId = teachers[1].Id
                }
            };
            res = snappy.SaveList(conn, classes);
            Assert.AreEqual(res, classes.Length);
            var calc = snappy.Query<Class>(conn, "select * from Class where Id='MAT443'");
            Assert.IsTrue(classes[1].Equals(calc));

            var student = new Student
            {
                Name = "Rune Aamodt",
                Gender = Gender.Male,
                Birthday = 19840321
            };
            res = snappy.Save(conn, student);
            Assert.AreEqual(res, 1);
            var me = snappy.Query<Student>(conn, "select * from Student where Id=@Id", ("Id", student.Id));
            Assert.IsTrue(student.Equals(me));
        }
    }
}
