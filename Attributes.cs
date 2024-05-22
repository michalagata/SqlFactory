using System;
using System.Diagnostics.CodeAnalysis;

namespace AnubisWorks.SQLFactory
{

    [AttributeUsage(AttributeTargets.Class)]
    sealed class DatabaseAttribute : Attribute
    {
        public string Name { get; set; }
    }

    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class TableAttribute : Attribute
    {
        public string Name { get; set; }
    }
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class ColumnAttribute : Attribute, IDataAttribute
    {
        bool canBeNull = true;
        bool canBeNullSet = false;
        public string Name { get; set; }
        string IDataAttribute.Storage { get; set; }
        [SuppressMessage("Microsoft.Naming", "CA1709:IdentifiersShouldBeCasedCorrectly", MessageId = "Db", Justification = "Conforms to legacy spelling.")]
        internal string DbType { get; set; }
        public Type ConvertTo { get; set; }
        internal string Expression { get; set; }
        public bool IsPrimaryKey { get; set; }
        [SuppressMessage("Microsoft.Naming", "CA1709:IdentifiersShouldBeCasedCorrectly", MessageId = "Db", Justification = "Conforms to legacy spelling.")]
        public bool IsDbGenerated { get; set; }
        public bool IsVersion { get; set; }
        internal UpdateCheck UpdateCheck { get; set; } = UpdateCheck.Always;
        public AutoSync AutoSync { get; set; } = AutoSync.Default;
        internal bool IsDiscriminator { get; set; }
        internal bool CanBeNull
        {
            get { return canBeNull; }
            set
            {
                canBeNullSet = true;
                canBeNull = value;
            }
        }
        internal bool CanBeNullSet => canBeNullSet;
    }

    internal enum UpdateCheck
    {
        Always,
        Never,
        WhenChanged
    }

    public enum AutoSync
    {
        Default = 0,
        Always = 1,
        Never = 2,
        OnInsert = 3,
        OnUpdate = 4
    }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class AssociationAttribute : Attribute, IDataAttribute
    {
        public string Name { get; set; }
        string IDataAttribute.Storage { get; set; }
        public string ThisKey { get; set; }
        public string OtherKey { get; set; }
        internal bool IsUnique { get; set; }
        internal bool IsForeignKey { get; set; }
        internal string DeleteRule { get; set; }
        internal bool DeleteOnNull { get; set; }
    }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
    sealed class InheritanceMappingAttribute : Attribute
    {
        public object Code { get; set; }
        [SuppressMessage("Microsoft.Naming", "CA1721:PropertyNamesShouldNotMatchGetMethods", Justification = "The contexts in which this is available are fairly specific.")]
        public Type Type { get; set; }
        public bool IsDefault { get; set; }
    }

    interface IDataAttribute
    {
        string Name { get; set; }
        string Storage { get; set; }
    }

    [AttributeUsage(AttributeTargets.Property)]
    public sealed class ComplexPropertyAttribute : Attribute
    {
        public string Name { get; set; }
        public string Separator { get; set; }
        public ComplexPropertyAttribute() { }
        internal ComplexPropertyAttribute(ComplexPropertyAttribute other)
        {
            this.Name = other.Name;
            this.Separator = other.Separator;
        }
    }
}