using System;

namespace SnappySql.Orm
{
    [AttributeUsage(AttributeTargets.Class)]
    public class Table : Attribute
    {
        public string Name { get; set; }
    }
}
