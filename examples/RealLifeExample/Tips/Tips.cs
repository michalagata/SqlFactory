using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RealLifeExample.Tips
{
    [AttributeUsage(AttributeTargets.Class)]
    public class DBProcedure : Attribute
    {

    }

    [AttributeUsage(AttributeTargets.Class)]
    public class DBTable : Attribute
    {
        public string SingleInstance = string.Empty;
        public string Custom = string.Empty;
        public bool GenerateView = true;
    }

    [AttributeUsage(AttributeTargets.Property)]
    public class DBTips : Attribute
    {
        public int Length = -1;
        public bool IsUnicode = true;
        public bool IsSmallDateTime = false;
        public bool IsFixedChar = false;
        public bool IsPrimaryKey = false;
        public bool IsIdentity = false;
        public bool IsText = false;
        public bool IsMoney = false;
        public bool IsUnique = false;
        public bool NotNull = false;
        public Type ForeignKey = null;
        public string Default = "";
        public string ForeignKeyField = string.Empty;
        public bool ForeignKeyOnDeleteCascade = false;
        public bool ForeignKeyOnUpdateCascade = false;
        /// <summary>
        /// Czy klucz obcy jest oddzielny
        /// </summary>
        public bool IsForeignKeySeparated = false;
        public string Description = string.Empty;
        public int Precision = -1;
        public int Scale = -1;
        public bool NOCHECK = false;
        public string Custom = string.Empty;
        public bool FindOrder = false;
    }

    [AttributeUsage(AttributeTargets.Class)]
    public class DBView : Attribute
    {
        public Type Table;
        public string SingleInstance = "";

        public DBView(Type table)
        {
            Table = table;
        }

        public DBView(Type table, String singleInstance)
        {
            SingleInstance = singleInstance;
            Table = table;
        }
    }

    [AttributeUsage(AttributeTargets.Property)]
    public sealed class ExternalDataSourceAttribute : Attribute
    {
    }

    [AttributeUsage(AttributeTargets.Class)]
    public class InterfaceFuncGenerate : Attribute
    {
        //public Int32 Functionss { get; set; }
        public string[] Functions { get; set; }
    }

    public interface ISqlDataProvider
    {
        object[] GetData();
    }

    [AttributeUsage(AttributeTargets.Class)]
    public sealed class ReturnAllRowsOnEmptyFindConditionsAttribute : Attribute
    {
    }

    public static class Settings
    {
        public const string DATABASEAPP = "BS_APP";
        public const string DATABASE = "BS_DM";
    }
}
