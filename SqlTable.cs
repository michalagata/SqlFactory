using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
namespace AnubisWorks.SQLFactory
{
    using Metadata;
    partial class Database
    {
        static readonly MethodInfo tableMethod = typeof(Database)
           .GetMethods(BindingFlags.Public | BindingFlags.Instance)
           .Single(m => m.Name == nameof(Table) && m.ContainsGenericParameters && m.GetParameters().Length == 0);
        static readonly MappingSource mappingSource = new AttributeMappingSource();
        readonly IDictionary<MetaType, SqlTable> tables = new Dictionary<MetaType, SqlTable>();
        readonly IDictionary<MetaType, ISqlTable> genericTables = new Dictionary<MetaType, ISqlTable>();
        partial void Initialize2(string providerInvariantName)
        {
            this.Configuration.SetModel(() => mappingSource.GetModel(GetType()));
            Initialize3(providerInvariantName);
        }
        partial void Initialize3(string providerInvariantName);
        public SqlTable<TEntity> Table<TEntity>() where TEntity : class
        {
            MetaType metaType = this.Configuration.GetMetaType(typeof(TEntity));
            ISqlTable set;
            SqlTable<TEntity> table;
            if (this.genericTables.TryGetValue(metaType, out set))
            {
                table = (SqlTable<TEntity>)set;
            }
            else
            {
                table = new SqlTable<TEntity>(this, metaType);
                this.genericTables.Add(metaType, table);
            }
            return table;
        }
        public SqlTable Table(Type entityType)
        {
            return Table(this.Configuration.GetMetaType(entityType));
        }
        internal SqlTable Table(MetaType metaType)
        {
            SqlTable table;
            if (!this.tables.TryGetValue(metaType, out table))
            {
                ISqlTable genericTable = (ISqlTable)
                   tableMethod.MakeGenericMethod(metaType.Type).Invoke(this, null);
                table = new SqlTable(this, metaType, genericTable);
                this.tables.Add(metaType, table);
            }
            return table;
        }
        public void Add(object entity)
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));
            Table(entity.GetType())
               .Add(entity);
        }
        public TEntity Find<TEntity>(object id) where TEntity : class
        {
            return Table<TEntity>()
               .Find(id);
        }
        public object Find(Type entityType, object id)
        {
            if (entityType == null) throw new ArgumentNullException(nameof(entityType));
            return Table(entityType)
               .Find(id);
        }
        public bool Contains(object entity)
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));
            return Table(entity.GetType())
               .Contains(entity);
        }
        public bool ContainsKey<TEntity>(object id) where TEntity : class
        {
            return Table<TEntity>()
               .ContainsKey(id);
        }
        public bool ContainsKey(Type entityType, object id)
        {
            if (entityType == null) throw new ArgumentNullException(nameof(entityType));
            return Table(entityType)
               .ContainsKey(id);
        }
        public void Update(object entity)
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));
            Table(entity.GetType())
               .Update(entity);
        }
        public void Update(object entity, object originalId)
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));
            Table(entity.GetType())
               .Update(entity, originalId);
        }
        public void Remove(object entity)
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));
            Table(entity.GetType())
               .Remove(entity);
        }
        public void RemoveKey<TEntity>(object id) where TEntity : class
        {
            Table<TEntity>()
               .RemoveKey(id);
        }
        public void RemoveKey(Type entityType, object id)
        {
            if (entityType == null) throw new ArgumentNullException(nameof(entityType));
            Table(entityType)
               .RemoveKey(id);
        }
        internal string BuildPredicateFragment(
              object entity,
              ICollection<MetaDataMember> predicateMembers,
              ICollection<object> parametersBuffer,
              Func<MetaDataMember, object> getValueFn = null)
        {
            var predicateValues = predicateMembers.ToDictionary(
               m => m.MappedName,
               m => (getValueFn != null) ? getValueFn(m) : m.GetValueForDatabase(entity)
            );
            return BuildPredicateFragment(predicateValues, parametersBuffer);
        }
        internal string BuildPredicateFragment(IDictionary<string, object> predicateValues, ICollection<object> parametersBuffer)
        {
            if (predicateValues == null || predicateValues.Count == 0) throw new ArgumentException("predicateValues cannot be empty", nameof(predicateValues));
            if (parametersBuffer == null) throw new ArgumentNullException(nameof(parametersBuffer));
            var sb = new StringBuilder();
            foreach (var item in predicateValues)
            {
                if (sb.Length > 0)
                {
                    sb.Append(" AND ");
                }
                sb.Append(QuoteIdentifier(item.Key));
                if (item.Value == null)
                {
                    sb.Append(" IS NULL");
                }
                else
                {
                    sb.Append(" = {")
                       .Append(parametersBuffer.Count)
                       .Append("}");
                    parametersBuffer.Add(item.Value);
                }
            }
            return sb.ToString();
        }
        internal string SelectBody(MetaType metaType, IEnumerable<MetaDataMember> selectMembers, string tableAlias)
        {
            if (selectMembers == null)
            {
                selectMembers = metaType.PersistentDataMembers.Where(m => !m.IsAssociation);
            }
            var sb = new StringBuilder();
            string qualifier = (!String.IsNullOrEmpty(tableAlias)) ?
               QuoteIdentifier(tableAlias) + "." : null;
            IEnumerator<MetaDataMember> enumerator = selectMembers.GetEnumerator();
            while (enumerator.MoveNext())
            {
                string mappedName = enumerator.Current.MappedName;
                string memberName = enumerator.Current.QueryPath;
                string columnAlias = !String.Equals(mappedName, memberName, StringComparison.Ordinal) ?
                   memberName : null;
                if (sb.Length > 0)
                {
                    sb.Append(", ");
                }
                if (qualifier != null)
                {
                    sb.Append(qualifier);
                }
                sb.Append(QuoteIdentifier(enumerator.Current.MappedName));
                if (columnAlias != null)
                {
                    sb.Append(" AS ")
                       .Append(QuoteIdentifier(memberName));
                }
            }
            return sb.ToString();
        }
        internal string FromBody(MetaType metaType, string tableAlias)
        {
            if (metaType.Table == null) throw new InvalidOperationException("metaType.Table cannot be null.");
            string alias = (!String.IsNullOrEmpty(tableAlias)) ?
               " " + QuoteIdentifier(tableAlias)
               : null;
            return QuoteIdentifier(metaType.Table.TableName) + (alias ?? "");
        }
    }
    sealed partial class DatabaseConfiguration
    {
        Lazy<MetaModel> _Model;
        MetaTableConfiguration _defaultMetaTableConfig;
        internal MetaModel Model => _Model.Value;
        public bool UseVersionMember { get; set; } = true;
        public bool EnableBatchCommands { get; set; } = true;
        public string DefaultComplexPropertySeparator { get; set; }
        internal MetaTableConfiguration DefaultMetaTableConfig
        {
            get
            {
                if (_defaultMetaTableConfig == null)
                {
                    _defaultMetaTableConfig = new MetaTableConfiguration
                    {
                        DefaultComplexPropertySeparator = this.DefaultComplexPropertySeparator
                    };
                }
                return _defaultMetaTableConfig;
            }
        }
        internal void SetModel(Func<MetaModel> modelFn)
        {
            _Model = new Lazy<MetaModel>(modelFn);
        }
        internal MetaType GetMetaType(Type type)
        {
            return this.Model.GetMetaType(type, this.DefaultMetaTableConfig);
        }
    }
    [DebuggerDisplay("{metaType.Name}")]
    public sealed class SqlTable : SqlSet, ISqlTable
    {
        readonly ISqlTable table;
        readonly MetaType metaType;
        public string Name
        {
            get { return metaType.Table.TableName; }
        }
        public SqlCommandBuilder<object> CommandBuilder { get; }
        internal SqlTable(Database db, MetaType metaType, ISqlTable table)
           : base(new string[2] { db.FromBody(metaType, null), db.SelectBody(metaType, null, null) }, metaType.Type, db)
        {
            this.table = table;
            this.metaType = metaType;
            this.CommandBuilder = new SqlCommandBuilder<object>(db, metaType);
        }
        public new SqlTable<TEntity> Cast<TEntity>() where TEntity : class
        {
            if (typeof(TEntity) != this.metaType.Type)
            {
                throw new InvalidOperationException("The specified type parameter is not valid for this instance.");
            }
            return (SqlTable<TEntity>)this.table;
        }
        [EditorBrowsable(EditorBrowsableState.Never)]
        public new SqlSet Cast(Type resultType)
        {
            return base.Cast(resultType);
        }
        internal static void EnsureEntityType(MetaType metaType)
        {
            if (!metaType.IsEntity)
            {
                throw new InvalidOperationException($"The operation is not available for non-entity types ('{metaType.Type.FullName}').");
            }
        }
        public void Add(object entity)
        {
            this.table.Add(entity);
        }
        void ISqlTable.AddDescendants(object entity)
        {
            this.table.AddDescendants(entity);
        }
        public void AddRange(IEnumerable<object> entities)
        {
            this.table.AddRange(entities);
        }
        public void AddRange(params object[] entities)
        {
            this.table.AddRange(entities);
        }
        public void Update(object entity)
        {
            this.table.Update(entity);
        }
        public void Update(object entity, object originalId)
        {
            this.table.Update(entity, originalId);
        }
        public void UpdateRange(IEnumerable<object> entities)
        {
            this.table.UpdateRange(entities);
        }
        public void UpdateRange(params object[] entities)
        {
            this.table.UpdateRange(entities);
        }
        public void Remove(object entity)
        {
            this.table.Remove(entity);
        }
        public void RemoveKey(object id)
        {
            this.table.RemoveKey(id);
        }
        public void RemoveRange(IEnumerable<object> entities)
        {
            this.table.RemoveRange(entities);
        }
        public void RemoveRange(params object[] entities)
        {
            this.table.RemoveRange(entities);
        }
        public new bool Contains(object entity)
        {
            return base.Contains(entity);
        }
        public new bool ContainsKey(object id)
        {
            return base.ContainsKey(id);
        }
        public void Refresh(object entity)
        {
            this.table.Refresh(entity);
        }
    }
    [DebuggerDisplay("{metaType.Name}")]
    public sealed class SqlTable<TEntity> : SqlSet<TEntity>, ISqlTable where TEntity : class
    {
        readonly MetaType metaType;
        public string Name
        {
            get { return metaType.Table.TableName; }
        }
        public SqlCommandBuilder<TEntity> CommandBuilder { get; }
        internal SqlTable(Database db, MetaType metaType)
           : base(new string[2] { db.FromBody(metaType, null), db.SelectBody(metaType, null, null) }, db)
        {
            this.metaType = metaType;
            this.CommandBuilder = new SqlCommandBuilder<TEntity>(db, metaType);
        }
        public void Add(TEntity entity)
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));
            SqlBuilder insertSql = this.CommandBuilder.BuildInsertStatementForEntity(entity);
            MetaDataMember idMember = this.metaType.DBGeneratedIdentityMember;
            MetaDataMember[] syncMembers =
               (from m in this.metaType.PersistentDataMembers
                where (m.AutoSync == AutoSync.Always || m.AutoSync == AutoSync.OnInsert)
                  && m != idMember
                select m).ToArray();
            using (var tx = this.db.EnsureInTransaction())
            {
                this.db.Execute(insertSql, affect: 1, exact: true);
                if (idMember != null)
                {
                    object id = this.db.LastInsertId();
                    object convertedId = Convert.ChangeType(id, idMember.Type, CultureInfo.InvariantCulture);
                    object entityObj = entity;
                    idMember.MemberAccessor.SetBoxedValue(ref entityObj, convertedId);
                }
                if (syncMembers.Length > 0
                   && this.metaType.IsEntity)
                {
                    Refresh(entity, syncMembers);
                }
                InsertDescendants(entity);
                tx.Commit();
            }
        }
        void InsertDescendants(TEntity entity)
        {
            InsertOneToOne(entity);
            InsertOneToMany(entity);
        }
        void InsertOneToOne(TEntity entity)
        {
            MetaAssociation[] oneToOne = this.metaType.Associations
               .Where(a => !a.IsMany && a.ThisKeyIsPrimaryKey && a.OtherKeyIsPrimaryKey)
               .ToArray();
            for (int i = 0; i < oneToOne.Length; i++)
            {
                MetaAssociation assoc = oneToOne[i];
                object child = assoc.ThisMember.MemberAccessor.GetBoxedValue(entity);
                if (child == null)
                {
                    continue;
                }
                for (int j = 0; j < assoc.ThisKey.Count; j++)
                {
                    MetaDataMember thisKey = assoc.ThisKey[j];
                    MetaDataMember otherKey = assoc.OtherKey[j];
                    object thisKeyVal = thisKey.MemberAccessor.GetBoxedValue(entity);
                    otherKey.MemberAccessor.SetBoxedValue(ref child, thisKeyVal);
                }
                SqlTable otherTable = this.db.Table(assoc.OtherType);
                otherTable.Add(child);
            }
        }
        void InsertOneToMany(TEntity entity)
        {
            MetaAssociation[] oneToMany = this.metaType.Associations.Where(a => a.IsMany).ToArray();
            for (int i = 0; i < oneToMany.Length; i++)
            {
                MetaAssociation assoc = oneToMany[i];
                object[] many = ((IEnumerable<object>)assoc.ThisMember.MemberAccessor.GetBoxedValue(entity) ?? new object[0])
                   .Where(o => o != null)
                   .ToArray();
                if (many.Length == 0) continue;
                for (int j = 0; j < many.Length; j++)
                {
                    object child = many[j];
                    for (int k = 0; k < assoc.ThisKey.Count; k++)
                    {
                        MetaDataMember thisKey = assoc.ThisKey[k];
                        MetaDataMember otherKey = assoc.OtherKey[k];
                        object thisKeyVal = thisKey.MemberAccessor.GetBoxedValue(entity);
                        otherKey.MemberAccessor.SetBoxedValue(ref child, thisKeyVal);
                    }
                }
                SqlTable otherTable = this.db.Table(assoc.OtherType);
                otherTable.AddRange(many);
                for (int j = 0; j < many.Length; j++)
                {
                    object child = many[j];
                    ((ISqlTable)otherTable).AddDescendants(child);
                }
            }
        }
        public void AddRange(IEnumerable<TEntity> entities)
        {
            if (entities == null) throw new ArgumentNullException(nameof(entities));
            AddRange(entities.ToArray());
        }
        public void AddRange(params TEntity[] entities)
        {
            if (entities == null) throw new ArgumentNullException(nameof(entities));
            entities = entities.Where(o => o != null).ToArray();
            if (entities.Length == 0)
            {
                return;
            }
            if (entities.Length == 1)
            {
                Add(entities[0]);
                return;
            }
            MetaDataMember[] syncMembers =
               (from m in this.metaType.PersistentDataMembers
                where (m.AutoSync == AutoSync.Always || m.AutoSync == AutoSync.OnInsert)
                select m).ToArray();
            bool batch = syncMembers.Length == 0
               && this.db.Configuration.EnableBatchCommands;
            if (batch)
            {
                SqlBuilder batchInsert = SqlBuilder.JoinSql(";" + Environment.NewLine, entities.Select(e => this.CommandBuilder.BuildInsertStatementForEntity(e)));
                using (var tx = this.db.EnsureInTransaction())
                {
                    this.db.Execute(batchInsert, affect: entities.Length, exact: true);
                    for (int i = 0; i < entities.Length; i++)
                    {
                        InsertDescendants(entities[i]);
                    }
                    tx.Commit();
                }
            }
            else
            {
                using (var tx = this.db.EnsureInTransaction())
                {
                    for (int i = 0; i < entities.Length; i++)
                    {
                        Add(entities[i]);
                    }
                    tx.Commit();
                }
            }
        }
        public void Update(TEntity entity)
        {
            Update(entity, null);
        }
        public void Update(TEntity entity, object originalId)
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));
            SqlBuilder updateSql = this.CommandBuilder.BuildUpdateStatementForEntity(entity, originalId);
            MetaDataMember[] syncMembers =
               (from m in this.metaType.PersistentDataMembers
                where m.AutoSync == AutoSync.Always || m.AutoSync == AutoSync.OnUpdate
                select m).ToArray();
            using (this.db.EnsureConnectionOpen())
            {
                this.db.Execute(updateSql, affect: 1, exact: true);
                if (syncMembers.Length > 0)
                {
                    Refresh(entity, syncMembers);
                }
            }
        }
        public void UpdateRange(IEnumerable<TEntity> entities)
        {
            if (entities == null) throw new ArgumentNullException(nameof(entities));
            UpdateRange(entities.ToArray());
        }
        public void UpdateRange(params TEntity[] entities)
        {
            if (entities == null) throw new ArgumentNullException(nameof(entities));
            entities = entities.Where(o => o != null).ToArray();
            if (entities.Length == 0)
            {
                return;
            }
            if (entities.Length == 1)
            {
                Update(entities[0]);
                return;
            }
            EnsureEntityType();
            MetaDataMember[] syncMembers =
               (from m in this.metaType.PersistentDataMembers
                where m.AutoSync == AutoSync.Always || m.AutoSync == AutoSync.OnUpdate
                select m).ToArray();
            bool batch = syncMembers.Length == 0
               && this.db.Configuration.EnableBatchCommands;
            if (batch)
            {
                SqlBuilder batchUpdate = SqlBuilder.JoinSql(";" + Environment.NewLine, entities.Select(e => this.CommandBuilder.BuildUpdateStatementForEntity(e)));
                this.db.Execute(batchUpdate, affect: entities.Length, exact: true);
            }
            else
            {
                using (var tx = this.db.EnsureInTransaction())
                {
                    for (int i = 0; i < entities.Length; i++)
                    {
                        Update(entities[i]);
                    }
                    tx.Commit();
                }
            }
        }
        public void Remove(TEntity entity)
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));
            SqlBuilder deleteSql = this.CommandBuilder.BuildDeleteStatementForEntity(entity);
            bool usingVersion = this.db.Configuration.UseVersionMember
               && this.metaType.VersionMember != null;
            this.db.Execute(deleteSql, affect: 1, exact: usingVersion);
        }
        public void RemoveKey(object id)
        {
            SqlBuilder deleteSql = this.CommandBuilder.BuildDeleteStatementForKey(id);
            this.db.Execute(deleteSql, affect: 1);
        }
        public void RemoveRange(IEnumerable<TEntity> entities)
        {
            if (entities == null) throw new ArgumentNullException(nameof(entities));
            RemoveRange(entities.ToArray());
        }
        public void RemoveRange(params TEntity[] entities)
        {
            if (entities == null) throw new ArgumentNullException(nameof(entities));
            entities = entities.Where(o => o != null).ToArray();
            if (entities.Length == 0)
            {
                return;
            }
            if (entities.Length == 1)
            {
                Remove(entities[0]);
                return;
            }
            EnsureEntityType();
            bool usingVersion = this.db.Configuration.UseVersionMember
               && this.metaType.VersionMember != null;
            bool singleStatement = this.metaType.IdentityMembers.Count == 1
               && !usingVersion;
            bool batch = this.db.Configuration.EnableBatchCommands;
            if (singleStatement)
            {
                MetaDataMember idMember = this.metaType.IdentityMembers[0];
                object[] ids = entities.Select(e => idMember.GetValueForDatabase(e)).ToArray();
                SqlBuilder sql = this.CommandBuilder
                   .BuildDeleteStatement()
                   .WHERE(this.db.QuoteIdentifier(idMember.MappedName) + " IN ({0})", SQL.List(ids));
                this.db.Execute(sql, affect: entities.Length);
            }
            else if (batch)
            {
                SqlBuilder batchDelete = SqlBuilder.JoinSql(";" + Environment.NewLine, entities.Select(e => this.CommandBuilder.BuildDeleteStatementForEntity(e)));
                this.db.Execute(batchDelete, affect: entities.Length, exact: usingVersion);
            }
            else
            {
                using (var tx = this.db.EnsureInTransaction())
                {
                    for (int i = 0; i < entities.Length; i++)
                    {
                        Remove(entities[i]);
                    }
                    tx.Commit();
                }
            }
        }
        public new bool Contains(TEntity entity)
        {
            return base.Contains(entity);
        }
        public new bool ContainsKey(object id)
        {
            return base.ContainsKey(id);
        }
        public void Refresh(TEntity entity)
        {
            Refresh(entity, null);
        }
        void Refresh(TEntity entity, IEnumerable<MetaDataMember> refreshMembers)
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));
            EnsureEntityType();
            SqlBuilder query = this.CommandBuilder.BuildSelectStatement(refreshMembers);
            query.WHERE(this.db.BuildPredicateFragment(entity, this.metaType.IdentityMembers, query.ParameterValues));
            Mapper mapper = this.db.CreatePocoMapper(this.metaType.Type);
            object entityObj = entity;
            this.db.Map<object>(query, r =>
            {
                mapper.Load(ref entityObj, r);
                return null;
            }).SingleOrDefault();
        }
        void EnsureEntityType()
        {
            SqlTable.EnsureEntityType(this.metaType);
        }
        void ISqlTable.Add(object entity)
        {
            Add((TEntity)entity);
        }
        void ISqlTable.AddDescendants(object entity)
        {
            InsertDescendants((TEntity)entity);
        }
        void ISqlTable.AddRange(IEnumerable<object> entities)
        {
            AddRange((IEnumerable<TEntity>)entities);
        }
        void ISqlTable.AddRange(params object[] entities)
        {
            if (entities == null) throw new ArgumentNullException(nameof(entities));
            AddRange(entities as TEntity[] ?? entities.Cast<TEntity>().ToArray());
        }
        void ISqlTable.Update(object entity)
        {
            Update((TEntity)entity);
        }
        void ISqlTable.Update(object entity, object originalId)
        {
            Update((TEntity)entity, originalId);
        }
        void ISqlTable.UpdateRange(IEnumerable<object> entities)
        {
            UpdateRange((IEnumerable<TEntity>)entities);
        }
        void ISqlTable.UpdateRange(params object[] entities)
        {
            if (entities == null) throw new ArgumentNullException(nameof(entities));
            UpdateRange(entities as TEntity[] ?? entities.Cast<TEntity>().ToArray());
        }
        void ISqlTable.Remove(object entity)
        {
            Remove((TEntity)entity);
        }
        void ISqlTable.RemoveKey(object id)
        {
            RemoveKey(id);
        }
        void ISqlTable.RemoveRange(IEnumerable<object> entities)
        {
            RemoveRange((IEnumerable<TEntity>)entities);
        }
        void ISqlTable.RemoveRange(params object[] entities)
        {
            if (entities == null) throw new ArgumentNullException(nameof(entities));
            RemoveRange(entities as TEntity[] ?? entities.Cast<TEntity>().ToArray());
        }
        void ISqlTable.Refresh(object entity)
        {
            Refresh((TEntity)entity);
        }
    }
    public sealed class SqlCommandBuilder<TEntity> where TEntity : class
    {
        readonly Database db;
        readonly MetaType metaType;
        internal SqlCommandBuilder(Database db, MetaType metaType)
        {
            this.db = db;
            this.metaType = metaType;
        }
        public SqlBuilder BuildSelectClause()
        {
            return BuildSelectClause(null);
        }
        public SqlBuilder BuildSelectClause(string tableAlias)
        {
            return BuildSelectClause(null, tableAlias);
        }
        SqlBuilder BuildSelectClause(IEnumerable<MetaDataMember> selectMembers, string tableAlias)
        {
            return new SqlBuilder()
               .SELECT(this.db.SelectBody(this.metaType, selectMembers, tableAlias));
        }
        public SqlBuilder BuildSelectStatement()
        {
            return BuildSelectStatement((string)null);
        }
        public SqlBuilder BuildSelectStatement(string tableAlias)
        {
            return BuildSelectStatement(null, tableAlias);
        }
        internal SqlBuilder BuildSelectStatement(IEnumerable<MetaDataMember> selectMembers, string tableAlias = null)
        {
            return BuildSelectClause(selectMembers, tableAlias)
               .FROM(this.db.FromBody(this.metaType, tableAlias));
        }
        public SqlBuilder BuildInsertStatementForEntity(TEntity entity)
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));
            MetaDataMember[] insertingMembers =
               (from m in this.metaType.PersistentDataMembers
                where !m.IsAssociation && !m.IsDbGenerated
                select m).ToArray();
            object[] parameters = insertingMembers
               .Select(m => m.GetValueForDatabase(entity))
               .ToArray();
            var sb = new StringBuilder()
               .Append("INSERT INTO ")
               .Append(QuoteIdentifier(this.metaType.Table.TableName))
               .Append(" (");
            for (int i = 0; i < insertingMembers.Length; i++)
            {
                if (i > 0)
                {
                    sb.Append(", ");
                }
                sb.Append(QuoteIdentifier(insertingMembers[i].MappedName));
            }
            sb.AppendLine(")")
               .Append("VALUES (");
            for (int i = 0; i < insertingMembers.Length; i++)
            {
                if (i > 0)
                {
                    sb.Append(", ");
                }
                sb.Append("{")
                   .Append(i)
                   .Append("}");
            }
            sb.Append(")");
            return new SqlBuilder(sb.ToString(), parameters);
        }
        public SqlBuilder BuildUpdateClause()
        {
            return new SqlBuilder("UPDATE " + QuoteIdentifier(this.metaType.Table.TableName));
        }
        public SqlBuilder BuildUpdateStatementForEntity(TEntity entity)
        {
            return BuildUpdateStatementForEntity(entity, null);
        }
        public SqlBuilder BuildUpdateStatementForEntity(TEntity entity, object originalId)
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));
            EnsureEntityType();
            MetaDataMember[] updatingMembers =
               (from m in this.metaType.PersistentDataMembers
                where !m.IsAssociation && !m.IsDbGenerated
                select m).ToArray();
            MetaDataMember[] predicateMembers =
               (from m in this.metaType.PersistentDataMembers
                where m.IsPrimaryKey || (m.IsVersion && this.db.Configuration.UseVersionMember)
                select m).ToArray();
            if (originalId != null
               && predicateMembers.Count(m => m.IsPrimaryKey) > 1)
            {
                throw new InvalidOperationException("The operation is not supported for entities with more than one identity member.");
            }
            var parametersBuffer = new List<object>(updatingMembers.Length + predicateMembers.Length);
            var sb = new StringBuilder()
               .Append("UPDATE ")
               .Append(QuoteIdentifier(this.metaType.Table.TableName))
               .AppendLine()
               .Append("SET ");
            for (int i = 0; i < updatingMembers.Length; i++)
            {
                if (i > 0)
                {
                    sb.Append(", ");
                }
                MetaDataMember member = updatingMembers[i];
                object value = member.GetValueForDatabase(entity);
                sb.Append(QuoteIdentifier(member.MappedName))
                   .Append(" = {")
                   .Append(parametersBuffer.Count)
                   .Append("}");
                parametersBuffer.Add(value);
            }
            Func<MetaDataMember, object> getValuefn = null;
            if (originalId != null)
            {
                getValuefn = m => (m.IsPrimaryKey) ?
                   m.ConvertValueForDatabase(originalId)
                   : m.GetValueForDatabase(entity);
            }
            sb.AppendLine()
               .Append("WHERE ")
               .Append(this.db.BuildPredicateFragment(entity, predicateMembers, parametersBuffer, getValuefn));
            return new SqlBuilder(sb.ToString(), parametersBuffer.ToArray());
        }
        public SqlBuilder BuildDeleteStatement()
        {
            return new SqlBuilder("DELETE FROM " + QuoteIdentifier(this.metaType.Table.TableName));
        }
        public SqlBuilder BuildDeleteStatementForEntity(TEntity entity)
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));
            EnsureEntityType();
            MetaDataMember[] predicateMembers =
               (from m in this.metaType.PersistentDataMembers
                where m.IsPrimaryKey || (m.IsVersion && this.db.Configuration.UseVersionMember)
                select m).ToArray();
            SqlBuilder deleteSql = BuildDeleteStatement();
            deleteSql.WHERE(this.db.BuildPredicateFragment(entity, predicateMembers, deleteSql.ParameterValues));
            return deleteSql;
        }
        public SqlBuilder BuildDeleteStatementForKey(object id)
        {
            EnsureEntityType();
            if (this.metaType.IdentityMembers.Count > 1)
            {
                throw new InvalidOperationException("Cannot call this method when the entity has more than one identity member.");
            }
            return BuildDeleteStatement()
               .WHERE(QuoteIdentifier(this.metaType.IdentityMembers[0].MappedName) + " = {0}", id);
        }
        string QuoteIdentifier(string unquotedIdentifier)
        {
            return this.db.QuoteIdentifier(unquotedIdentifier);
        }
        void EnsureEntityType()
        {
            SqlTable.EnsureEntityType(this.metaType);
        }
        [EditorBrowsable(EditorBrowsableState.Never)]
        public override bool Equals(object obj)
        {
            return base.Equals(obj);
        }
        [EditorBrowsable(EditorBrowsableState.Never)]
        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
        [EditorBrowsable(EditorBrowsableState.Never)]
        [SuppressMessage("Microsoft.Design", "CA1024:UsePropertiesWhereAppropriate", Justification = "Must match base signature.")]
        public new Type GetType()
        {
            return base.GetType();
        }
        [EditorBrowsable(EditorBrowsableState.Never)]
        public override string ToString()
        {
            return base.ToString();
        }
    }
    partial class SqlSet
    {
        MetaType EnsureEntityType(int maxIdMembers = -1)
        {
            Type resultType = this.ResultType;
            if (resultType == null)
            {
                throw new InvalidOperationException("The operation is not supported on untyped sets.");
            }
            MetaType metaType = this.db.Configuration.GetMetaType(resultType);
            if (metaType == null)
            {
                throw new InvalidOperationException($"Mapping information was not found for '{resultType.FullName}'.");
            }
            SqlTable.EnsureEntityType(metaType);
            if (maxIdMembers > 0
               && metaType.IdentityMembers.Count > maxIdMembers)
            {
                throw new InvalidOperationException("The operation is not supported for entities with more than one identity member.");
            }
            return metaType;
        }
        public bool Contains(object entity)
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));
            MetaType metaType = EnsureEntityType();
            MetaDataMember[] predicateMembers =
               (from m in metaType.PersistentDataMembers
                where m.IsPrimaryKey || (m.IsVersion && this.db.Configuration.UseVersionMember)
                select m).ToArray();
            IDictionary<string, object> predicateValues = predicateMembers.ToDictionary(
               m => m.MappedName,
               m => m.GetValueForDatabase(entity)
            );
            return ContainsImpl(predicateMembers, predicateValues);
        }
        public bool ContainsKey(object id)
        {
            MetaType metaType = EnsureEntityType(maxIdMembers: 1);
            MetaDataMember idMember = metaType.IdentityMembers[0];
            MetaDataMember[] predicateMembers = new[] { idMember };
            var predicateValues = new Dictionary<string, object> {
            { idMember.MappedName, idMember.ConvertValueForDatabase(id) }
         };
            return ContainsImpl(predicateMembers, predicateValues);
        }
        bool ContainsImpl(MetaDataMember[] predicateMembers, IDictionary<string, object> predicateValues)
        {
            MetaType metaType = predicateMembers[0].DeclaringType;
            var predicateParams = new List<object>(predicateValues.Count);
            return Where(this.db.BuildPredicateFragment(predicateValues, predicateParams), predicateParams.ToArray())
               .Select(this.db.SelectBody(metaType, predicateMembers, null))
               .Any();
        }
        public object Find(object id)
        {
            return FindImpl(id).SingleOrDefault();
        }
        internal SqlSet FindImpl(object id)
        {
            if (id == null) throw new ArgumentNullException(nameof(id));
            MetaType metaType = EnsureEntityType(maxIdMembers: 1);
            MetaDataMember idMember = metaType.IdentityMembers[0];
            var predicateValues = new Dictionary<string, object> {
            { idMember.MappedName, idMember.ConvertValueForDatabase(id) }
         };
            var parameters = new List<object>(predicateValues.Count);
            string predicate = db.BuildPredicateFragment(predicateValues, parameters);
            return Where(predicate, parameters.ToArray());
        }
        public SqlSet Include(string path)
        {
            if (path == null) throw new ArgumentNullException(nameof(path));
            if (this.ResultType == null)
            {
                throw new InvalidOperationException("Include operation is not supported on untyped sets.");
            }
            MetaType metaType = this.db.Configuration.GetMetaType(this.ResultType);
            if (metaType == null)
            {
                throw new InvalidOperationException($"Mapping information was not found for '{this.ResultType.FullName}'.");
            }
            return IncludeImpl.Expand(this, path, metaType);
        }
        static class IncludeImpl
        {
            static readonly char[] _pathSeparator = { '.' };
            public static SqlSet Expand(SqlSet source, string path, MetaType metaType)
            {
                Database db = source.db;
                string[] parts = path.Split(_pathSeparator);
                Func<string, SqlBuilder> selectBuild = alias =>
                   new SqlBuilder().SELECT(db.QuoteIdentifier(alias) + ".*");
                Action<SqlBuilder, string> fromAppend = (sql, alias) =>
                   sql.FROM(source.GetDefiningQuery(), db.QuoteIdentifier(alias));
                MetaAssociation manyAssoc;
                int manyIndex;
                SqlBuilder query = BuildJoinedQuery(parts, metaType, source.db, selectBuild, fromAppend, out manyAssoc, out manyIndex);
                SqlSet newSet = (query == null) ? source.Clone() : source.CreateSet(query);
                if (manyAssoc != null)
                {
                    AddManyInclude(newSet, parts, path, manyAssoc, manyIndex);
                }
                return newSet;
            }
            static SqlBuilder BuildJoinedQuery(
                  string[] path, MetaType metaType, Database db,
                  Func<string, SqlBuilder> selectBuild, Action<SqlBuilder, string> fromAppend,
                  out MetaAssociation manyAssoc, out int manyIndex)
            {
                manyAssoc = null;
                manyIndex = -1;
                const string leftAlias = "dbex_l";
                const string rightAlias = "dbex_r";
                Func<int, string> rAliasFn = i => rightAlias + (i + 1);
                SqlBuilder query = selectBuild(leftAlias);
                MetaType currentType = metaType;
                var associations = new List<MetaAssociation>();
                for (int i = 0; i < path.Length; i++)
                {
                    string p = path[i];
                    string rAlias = rAliasFn(i);
                    MetaDataMember member = currentType.PersistentDataMembers.SingleOrDefault(m => m.Name == p);
                    if (member == null)
                    {
                        throw new ArgumentException($"Couldn't find '{p}' on '{currentType.Type.FullName}'.", nameof(path));
                    }
                    if (!member.IsAssociation)
                    {
                        throw new ArgumentException($"'{p}' is not an association property.", nameof(path));
                    }
                    MetaAssociation association = member.Association;
                    if (association.IsMany)
                    {
                        manyAssoc = association;
                        manyIndex = i;
                        break;
                    }
                    associations.Add(association);
                    query.SELECT(String.Join(", ", association.OtherType.PersistentDataMembers
                       .Where(m => !m.IsAssociation)
                       .Select(m => $"{db.QuoteIdentifier(rAlias)}.{db.QuoteIdentifier(m.MappedName)} AS {String.Join("$", associations.Select(a => a.ThisMember.Name))}${m.Name}")));
                    currentType = association.OtherType;
                }
                if (associations.Count == 0)
                {
                    return null;
                }
                fromAppend(query, leftAlias);
                for (int i = 0; i < associations.Count; i++)
                {
                    MetaAssociation association = associations[i];
                    string lAlias = (i == 0) ? leftAlias : rAliasFn(i - 1);
                    string rAlias = rAliasFn(i);
                    var joinPredicate = new StringBuilder();
                    for (int j = 0; j < association.ThisKey.Count; j++)
                    {
                        if (j > 0)
                        {
                            joinPredicate.Append(" AND ");
                        }
                        MetaDataMember thisMember = association.ThisKey[j];
                        MetaDataMember otherMember = association.OtherKey[j];
                        joinPredicate.AppendFormat(CultureInfo.InvariantCulture, "{0}.{1} = {2}.{3}", db.QuoteIdentifier(lAlias), db.QuoteIdentifier(thisMember.Name), db.QuoteIdentifier(rAlias), db.QuoteIdentifier(otherMember.MappedName));
                    }
                    query.LEFT_JOIN($"{db.QuoteIdentifier(association.OtherType.Table.TableName)} {db.QuoteIdentifier(rAlias)} ON ({joinPredicate.ToString()})");
                }
                return query;
            }
            static void AddManyInclude(SqlSet set, string[] path, string originalPath, MetaAssociation manyAssoc, int manyIndex)
            {
                Debug.Assert(path.Length > 0);
                Debug.Assert(manyIndex >= 0);
                Database db = set.db;
                MetaType metaType = manyAssoc.OtherType;
                SqlTable table = db.Table(metaType);
                string[] manyPath;
                SqlSet manySource;
                if (manyIndex == path.Length - 1)
                {
                    manyPath = path;
                    manySource = table;
                }
                else
                {
                    manyPath = new string[manyIndex + 1];
                    Array.Copy(path, manyPath, manyPath.Length);
                    string[] manyInclude = new string[path.Length - manyIndex - 1];
                    Array.Copy(path, manyIndex + 1, manyInclude, 0, manyInclude.Length);
                    Func<string, SqlBuilder> selectBuild = alias =>
                       table.CommandBuilder.BuildSelectClause(alias);
                    Action<SqlBuilder, string> fromAppend = (sql, alias) =>
                       sql.FROM(db.QuoteIdentifier(metaType.Table.TableName) + " " + alias);
                    MetaAssociation manyInManyAssoc;
                    int manyInManyIndex;
                    SqlBuilder manyQuery = BuildJoinedQuery(manyInclude, metaType, db, selectBuild, fromAppend, out manyInManyAssoc, out manyInManyIndex);
                    if (manyInManyAssoc != null)
                    {
                        throw new ArgumentException($"One-to-many associations can only be specified once in an include path ('{originalPath}').", nameof(path));
                    }
                    manySource = db.From(manyQuery, metaType.Type);
                }
                if (set.ManyIncludes == null)
                {
                    set.ManyIncludes = new Dictionary<string[], CollectionLoader>();
                }
                set.ManyIncludes.Add(manyPath, new CollectionLoader
                {
                    Load = GetMany,
                    State = new CollectionLoaderState
                    {
                        Source = manySource,
                        Association = manyAssoc
                    }
                });
            }
            static IEnumerable GetMany(object container, object state)
            {
                var loaderState = (CollectionLoaderState)state;
                MetaAssociation association = loaderState.Association;
                SqlSet set = loaderState.Source;
                var predicateValues = new Dictionary<string, object>();
                for (int i = 0; i < association.OtherKey.Count; i++)
                {
                    predicateValues.Add(association.OtherKey[i].MappedName, association.ThisKey[i].GetValueForDatabase(container));
                }
                var parameters = new List<object>();
                string whereFragment = set.db.BuildPredicateFragment(predicateValues, parameters);
                IEnumerable children = set.Where(whereFragment, parameters.ToArray()).AsEnumerable();
                MetaDataMember otherMember = association.OtherMember;
                foreach (object child in children)
                {
                    if (otherMember != null
                       && !otherMember.Association.IsMany)
                    {
                        object childObj = child;
                        otherMember.MemberAccessor.SetBoxedValue(ref childObj, container);
                    }
                    yield return child;
                }
            }
            class CollectionLoaderState
            {
                public SqlSet Source;
                public MetaAssociation Association;
            }
        }
    }
    partial class SqlSet<TResult>
    {
        [EditorBrowsable(EditorBrowsableState.Never)]
        public new bool Contains(object entity)
        {
            return Contains((TResult)entity);
        }
        public bool Contains(TResult entity)
        {
            return base.Contains(entity);
        }
        [SuppressMessage("Microsoft.Design", "CA1011:ConsiderPassingBaseTypesAsParameters", Justification = "Need to keep result type same as input type.")]
        public new TResult Find(object id)
        {
            return ((SqlSet<TResult>)FindImpl(id)).SingleOrDefault();
        }
        [SuppressMessage("Microsoft.Design", "CA1011:ConsiderPassingBaseTypesAsParameters", Justification = "Need to keep result type same as input type.")]
        public new SqlSet<TResult> Include(string path)
        {
            return (SqlSet<TResult>)base.Include(path);
        }
    }
    interface ISqlTable
    {
        string Name { get; }
        void Remove(object entity);
        void RemoveKey(object id);
        void RemoveRange(IEnumerable<object> entities);
        void RemoveRange(params object[] entities);
        void Add(object entity);
        void AddDescendants(object entity);
        void AddRange(IEnumerable<object> entities);
        void AddRange(params object[] entities);
        void Refresh(object entity);
        void Update(object entity);
        void Update(object entity, object originalId);
        void UpdateRange(IEnumerable<object> entities);
        void UpdateRange(params object[] entities);
    }
}