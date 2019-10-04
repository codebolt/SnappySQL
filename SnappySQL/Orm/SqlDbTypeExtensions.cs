using System.Data;

namespace SnappySql.Orm
{
    static public class SqlDbTypeExtensions
    {
        static public bool IsDateType(this SqlDbType dbType) =>
            (dbType == SqlDbType.Date || dbType == SqlDbType.DateTime
           || dbType == SqlDbType.DateTime2 || dbType == SqlDbType.SmallDateTime);

        static public bool IsStringType(this SqlDbType dbType) =>
            (dbType == SqlDbType.Char || dbType == SqlDbType.NChar ||
            dbType == SqlDbType.NVarChar || dbType == SqlDbType.NText ||
            dbType == SqlDbType.Text || dbType == SqlDbType.VarChar ||
            dbType == SqlDbType.Xml);
    }
}
