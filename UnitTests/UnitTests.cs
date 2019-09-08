using Microsoft.SqlServer.Dac;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SnappySql;
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

        private readonly SnappyEngine snappy = new SnappyEngine(connectionString);

        [TestInitialize]
        public void InitTestDB()
        {
            var dacServices = new DacServices(connectionString);
            string dacPath = Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "TestDB.dacpac");
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
                conn.ChangeDatabase("master");
                using var cmd = new SqlCommand("drop database SnappySqlUnitTestDB", conn);
                cmd.ExecuteNonQuery();
            }
            catch(SqlException) { }
        }

        [TestMethod]
        public void CompositeTest()
        {
            using var conn = snappy.ConnectionFactory.GetConnection();
            conn.Open();
            var teacher = new Teacher
            {
                Name = "John Doe",
                Address = "Moses Rd 12",
                Telephone = "99991111",
                Email = "noreply@test.com",
            };
            snappy.Save(conn, teacher);
            Assert.IsTrue(teacher.Id > 0);
            var list = snappy.QueryList<Teacher>("select * from Teacher");
            Assert.IsTrue(list.Any(t => t.Equals(teacher)));
        }
    }
}
