using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
namespace AnubisWorks.SQLFactory.Metadata
{
    abstract class MetaModel
    {
        internal abstract MappingSource MappingSource { get; }
        internal abstract Type ContextType { get; }
        internal abstract string DatabaseName { get; }
        public abstract MetaTable GetTable(Type rowType, MetaTableConfiguration config);
        [SuppressMessage("Microsoft.Design", "CA1024:UsePropertiesWhereAppropriate", Justification = "Non-trivial operations are not suitable for properties.")]
        public abstract IEnumerable<MetaTable> GetTables();
        public abstract MetaType GetMetaType(Type type, MetaTableConfiguration config);
    }
    abstract class MetaTable
    {
        public abstract MetaModel Model { get; }
        public abstract string TableName { get; }
        public abstract MetaType RowType { get; }
    }
    class MetaTableConfiguration
    {
        public string DefaultComplexPropertySeparator { get; internal set; }
        public MetaTableConfiguration() { }
        internal MetaTableConfiguration(MetaTableConfiguration other)
        {
            this.DefaultComplexPropertySeparator = other.DefaultComplexPropertySeparator;
        }
    }
    abstract class MetaType
    {
        public abstract MetaModel Model { get; }
        public abstract MetaTable Table { get; }
        [SuppressMessage("Microsoft.Naming", "CA1721:PropertyNamesShouldNotMatchGetMethods", Justification = "The contexts in which this is available are fairly specific.")]
        public abstract Type Type { get; }
        public abstract string Name { get; }
        public abstract bool IsEntity { get; }
        public abstract bool CanInstantiate { get; }
        public abstract MetaDataMember DBGeneratedIdentityMember { get; }
        public abstract MetaDataMember VersionMember { get; }
        internal abstract MetaDataMember Discriminator { get; }
        public abstract bool HasUpdateCheck { get; }
        internal abstract bool HasInheritance { get; }
        internal abstract bool HasInheritanceCode { get; }
        internal abstract object InheritanceCode { get; }
        internal abstract bool IsInheritanceDefault { get; }
        internal abstract MetaType InheritanceRoot { get; }
        internal abstract MetaType InheritanceBase { get; }
        internal abstract MetaType InheritanceDefault { get; }
        internal abstract MetaType GetInheritanceType(Type type);
        internal abstract MetaType GetTypeForInheritanceCode(object code);
        internal abstract ReadOnlyCollection<MetaType> InheritanceTypes { get; }
        internal abstract ReadOnlyCollection<MetaType> DerivedTypes { get; }
        public abstract ReadOnlyCollection<MetaDataMember> DataMembers { get; }
        public abstract ReadOnlyCollection<MetaDataMember> PersistentDataMembers { get; }
        public abstract ReadOnlyCollection<MetaDataMember> IdentityMembers { get; }
        public abstract ReadOnlyCollection<MetaAssociation> Associations { get; }
        public abstract MetaDataMember GetDataMember(MemberInfo member);
    }
    [SuppressMessage("Microsoft.Naming", "CA1702:CompoundWordsShouldBeCasedCorrectly", MessageId = "MetaData", Justification = "The capitalization was deliberately chosen.")]
    abstract class MetaDataMember
    {
        public abstract MetaType DeclaringType { get; }
        public abstract MemberInfo Member { get; }
        public abstract MemberInfo StorageMember { get; }
        public abstract string Name { get; }
        public abstract string MappedName { get; }
        public virtual string QueryPath => Name;
        public abstract int Ordinal { get; }
        [SuppressMessage("Microsoft.Naming", "CA1721:PropertyNamesShouldNotMatchGetMethods", Justification = "The contexts in which this is available are fairly specific.")]
        public abstract Type Type { get; }
        public abstract Type ConvertToType { get; }
        public abstract bool IsDeclaredBy(MetaType type);
        public abstract MetaAccessor MemberAccessor { get; }
        public abstract MetaAccessor StorageAccessor { get; }
        public abstract bool IsPersistent { get; }
        public abstract bool IsAssociation { get; }
        public abstract bool IsPrimaryKey { get; }
        [SuppressMessage("Microsoft.Naming", "CA1709:IdentifiersShouldBeCasedCorrectly", MessageId = "Db", Justification = "Conforms to legacy spelling.")]
        public abstract bool IsDbGenerated { get; }
        public abstract bool IsVersion { get; }
        internal abstract bool IsDiscriminator { get; }
        public abstract bool CanBeNull { get; }
        [SuppressMessage("Microsoft.Naming", "CA1709:IdentifiersShouldBeCasedCorrectly", MessageId = "Db", Justification = "Conforms to legacy spelling.")]
        public abstract string DbType { get; }
        public abstract string Expression { get; }
        public abstract UpdateCheck UpdateCheck { get; }
        public abstract AutoSync AutoSync { get; }
        public abstract MetaAssociation Association { get; }
        public virtual object GetValueForDatabase(object instance)
        {
            object value = this.MemberAccessor.GetBoxedValue(instance);
            return ConvertValueForDatabase(value);
        }
        public object ConvertValueForDatabase(object value)
        {
            if (value == null
               || this.ConvertToType == null)
            {
                return value;
            }
            return Convert.ChangeType(value, this.ConvertToType);
        }
    }
    abstract class MetaAssociation
    {
        public abstract MetaType OtherType { get; }
        public abstract MetaDataMember ThisMember { get; }
        public abstract MetaDataMember OtherMember { get; }
        public abstract ReadOnlyCollection<MetaDataMember> ThisKey { get; }
        public abstract ReadOnlyCollection<MetaDataMember> OtherKey { get; }
        public abstract bool IsMany { get; }
        public abstract bool IsForeignKey { get; }
        public abstract bool IsUnique { get; }
        public abstract bool IsNullable { get; }
        public abstract bool ThisKeyIsPrimaryKey { get; }
        public abstract bool OtherKeyIsPrimaryKey { get; }
        public abstract string DeleteRule { get; }
        public abstract bool DeleteOnNull { get; }
    }
    abstract class MetaAccessor
    {
        [SuppressMessage("Microsoft.Naming", "CA1721:PropertyNamesShouldNotMatchGetMethods", Justification = "The contexts in which this is available are fairly specific.")]
        public abstract Type Type { get; }
        public abstract object GetBoxedValue(object instance);
        [SuppressMessage("Microsoft.Design", "CA1007:UseGenericsWhereAppropriate", Justification = "[....]: Needs to handle classes and structs.")]
        [SuppressMessage("Microsoft.Design", "CA1045:DoNotPassTypesByReference", Justification = "Unknown reason.")]
        public abstract void SetBoxedValue(ref object instance, object value);
        internal virtual bool HasValue(object instance)
        {
            return true;
        }
        internal virtual bool HasAssignedValue(object instance)
        {
            return true;
        }
        internal virtual bool HasLoadedValue(object instance)
        {
            return false;
        }
    }
    abstract class MetaAccessor<TEntity, TMember> : MetaAccessor
    {
        public override Type Type => typeof(TMember);
        public override void SetBoxedValue(ref object instance, object value)
        {
            TEntity tInst = (TEntity)instance;
            SetValue(ref tInst, (TMember)value);
            instance = tInst;
        }
        public override object GetBoxedValue(object instance)
        {
            return GetValue((TEntity)instance);
        }
        public abstract TMember GetValue(TEntity instance);
        [SuppressMessage("Microsoft.Design", "CA1045:DoNotPassTypesByReference", MessageId = "0#", Justification = "Unknown reason.")]
        public abstract void SetValue(ref TEntity instance, TMember value);
    }
}