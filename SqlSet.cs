using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Collections;
namespace AnubisWorks.SQLFactory
{
    partial class SqlBuilder
    {
        public SqlBuilder WITH(SqlSet subQuery, string alias)
        {
            return WITH(alias + " AS ({0})", subQuery);
        }
        public SqlBuilder FROM(SqlSet subQuery, string alias)
        {
            return FROM("({0}) " + alias, subQuery);
        }
        partial void GetDefiningQueryFromObject(object obj, ref SqlBuilder definingQuery)
        {
            definingQuery = (obj as SqlSet)?.GetDefiningQuery();
        }
    }
    static partial class SQL
    {
        public static SqlBuilder WITH(SqlSet subQuery, string alias)
        {
            return new SqlBuilder().WITH(subQuery, alias);
        }
    }
    partial class Database
    {
        public SqlSet From(string tableName)
        {
            return From(tableName, null);
        }
        public SqlSet From(string tableName, Type resultType)
        {
            return new SqlSet(new string[2] { tableName, null }, resultType, this);
        }
        public SqlSet<TResult> From<TResult>(string tableName)
        {
            return new SqlSet<TResult>(new string[2] { tableName, null }, this);
        }
        public SqlSet From(SqlBuilder definingQuery)
        {
            return From(definingQuery, null);
        }
        public SqlSet From(SqlBuilder definingQuery, Type resultType)
        {
            return new SqlSet(definingQuery, resultType, this);
        }
        public SqlSet<TResult> From<TResult>(SqlBuilder definingQuery)
        {
            return new SqlSet<TResult>(definingQuery, this);
        }
        public SqlSet<TResult> From<TResult>(SqlBuilder definingQuery, Func<IDataRecord, TResult> mapper)
        {
            return new SqlSet<TResult>(definingQuery, mapper, this);
        }
    }
    public partial class SqlSet : ISqlSet<SqlSet, object>
    {
        readonly SqlBuilder definingQuery;
        readonly string[] fromSelect;
        readonly SqlBuffer buffer;
        internal readonly Database db;
        readonly int setIndex = 1;
        public Type ResultType { get; }
        internal SqlSet(SqlBuilder definingQuery, Type resultType, Database db)
        {
            if (definingQuery == null) throw new ArgumentNullException(nameof(definingQuery));
            this.definingQuery = definingQuery.Clone();
            this.ResultType = resultType;
            this.db = db;
        }
        internal SqlSet(string[] fromSelect, Type resultType, Database db)
        {
            if (fromSelect == null) throw new ArgumentNullException(nameof(fromSelect));
            if (fromSelect.Length != 2) throw new ArgumentException("fromSelect.Length must be 2.", nameof(fromSelect));
            this.fromSelect = fromSelect;
            this.ResultType = resultType;
            this.db = db;
        }
        internal SqlSet(SqlSet set, SqlBuilder superQuery, Type resultType, SqlBuffer? buffer)
           : this(set, resultType, buffer)
        {
            if (superQuery == null) throw new ArgumentNullException(nameof(superQuery));
            this.definingQuery = superQuery;
        }
        internal SqlSet(SqlSet set, string[] fromSelect, Type resultType, SqlBuffer? buffer)
           : this(set, resultType, buffer)
        {
            if (fromSelect == null) throw new ArgumentNullException(nameof(fromSelect));
            if (fromSelect.Length != 2) throw new ArgumentException("fromSelect.Length must be 2.", nameof(fromSelect));
            this.fromSelect = fromSelect;
        }
        private SqlSet(SqlSet set, Type resultType, SqlBuffer? buffer)
        {
            if (set == null) throw new ArgumentNullException(nameof(set));
            this.ResultType = set.ResultType;
            this.setIndex += set.setIndex;
            this.db = set.db;
            if (resultType != null)
            {
                this.ResultType = resultType;
            }
            if (buffer != null)
            {
                this.buffer = buffer.Value;
            }
            Initialize2(set);
        }
        partial void Initialize2(SqlSet set);
        [SuppressMessage("Microsoft.Design", "CA1024:UsePropertiesWhereAppropriate", Justification = "Calling the member twice in succession creates different results.")]
        public SqlBuilder GetDefiningQuery()
        {
            return GetDefiningQuery(clone: true);
        }
        internal SqlBuilder GetDefiningQuery(bool clone = true, bool ignoreBuffer = false, bool super = false, string selectFormat = null, object[] args = null)
        {
            if (!ignoreBuffer
               && this.buffer.HasBuffer)
            {
                return BuildQuery(selectFormat, args);
            }
            SqlBuilder query = this.definingQuery;
            if (query == null)
            {
                query = new SqlBuilder()
                   .SELECT(selectFormat ?? this.fromSelect[1] ?? "*", args)
                   .FROM(this.fromSelect[0]);
            }
            else if (super || selectFormat != null)
            {
                query = CreateSuperQuery(query, selectFormat, args);
            }
            else if (clone)
            {
                query = query.Clone();
            }
            return query;
        }
        SqlBuilder BuildQuery(string selectFormat, object[] args)
        {
            switch (this.db.Configuration.SqlDialect)
            {
                case SqlDialect.Default:
                    return BuildQuery_Default(selectFormat, args);
                case SqlDialect.TSql:
                    return BuildQuery_TSql(selectFormat, args);
                default:
                    throw new NotImplementedException();
            }
        }
        SqlBuilder BuildQuery_Default(string selectFormat, object[] args)
        {
            SqlFragment whereBuffer = this.buffer.Where;
            SqlFragment orderByBuffer = this.buffer.OrderBy;
            int? skipBuffer = this.buffer.Skip;
            int? takeBuffer = this.buffer.Take;
            bool hasWhere = whereBuffer != null;
            bool hasOrderBy = orderByBuffer != null;
            bool hasSkip = skipBuffer.HasValue;
            bool hasTake = takeBuffer.HasValue;
            SqlBuilder query = GetDefiningQuery(ignoreBuffer: true, super: true, selectFormat: selectFormat, args: args);
            if (hasWhere
               || hasOrderBy
               || hasTake
               || hasSkip)
            {
                if (hasWhere)
                {
                    query.WHERE(whereBuffer.Format, whereBuffer.Args);
                }
                if (hasOrderBy)
                {
                    query.ORDER_BY(orderByBuffer.Format, orderByBuffer.Args);
                }
                if (hasTake)
                {
                    query.LIMIT(takeBuffer.Value);
                }
                if (hasSkip)
                {
                    query.OFFSET(skipBuffer.Value);
                }
            }
            return query;
        }
        SqlBuilder BuildQuery_TSql(string selectFormat, object[] args)
        {
            SqlFragment whereBuffer = this.buffer.Where;
            SqlFragment orderByBuffer = this.buffer.OrderBy;
            int? skipBuffer = this.buffer.Skip;
            int? takeBuffer = this.buffer.Take;
            bool hasWhere = whereBuffer != null;
            bool hasOrderBy = orderByBuffer != null;
            bool hasSkip = skipBuffer.HasValue;
            bool hasTake = takeBuffer.HasValue;
            if (hasSkip)
            {
                SqlBuilder query = GetDefiningQuery(ignoreBuffer: true, super: true, selectFormat: selectFormat, args: args);
                if (hasWhere)
                {
                    query.WHERE(whereBuffer.Format, whereBuffer.Args);
                }
                if (hasOrderBy)
                {
                    query.ORDER_BY(orderByBuffer.Format, orderByBuffer.Args);
                }
                else
                {
                    query.ORDER_BY("1");
                }
                query.OFFSET("{0} ROWS", skipBuffer.Value);
                if (hasTake)
                {
                    query.AppendClause("FETCH", null, "NEXT {0} ROWS ONLY", takeBuffer.Value);
                }
                return query;
            }
            else if (hasTake)
            {
                SqlBuilder query = GetDefiningQuery(ignoreBuffer: true, super: true, selectFormat: "TOP({0}) *", args: new object[1] { takeBuffer.Value });
                if (hasWhere)
                {
                    query.WHERE(whereBuffer.Format, whereBuffer.Args);
                }
                if (hasOrderBy)
                {
                    query.ORDER_BY(orderByBuffer.Format, orderByBuffer.Args);
                }
                if (selectFormat != null)
                {
                    query = CreateSuperQuery(query, selectFormat, args);
                }
                return query;
            }
            else
            {
                SqlBuilder query = GetDefiningQuery(ignoreBuffer: true, super: true, selectFormat: selectFormat, args: args);
                if (hasWhere)
                {
                    query.WHERE(whereBuffer.Format, whereBuffer.Args);
                }
                if (hasOrderBy)
                {
                    query.ORDER_BY(orderByBuffer.Format, orderByBuffer.Args);
                    query.OFFSET("0 ROWS");
                }
                return query;
            }
        }
        SqlBuilder CreateSuperQuery(SqlBuilder query, string selectFormat, object[] args)
        {
            var superQuery = new SqlBuilder()
               .SELECT(selectFormat ?? "*", args)
               .FROM(query, "dbex_set" + this.setIndex.ToString(CultureInfo.InvariantCulture));
            return superQuery;
        }
        internal virtual SqlSet CreateSet(SqlBuilder superQuery, Type resultType = null, SqlBuffer? buffer = null)
        {
            return new SqlSet(this, superQuery, resultType, buffer);
        }
        internal virtual SqlSet CreateSet(string[] fromSelect, Type resultType = null, SqlBuffer? buffer = null)
        {
            return new SqlSet(this, fromSelect, resultType, buffer);
        }
        internal SqlSet<TResult> CreateSet<TResult>(SqlBuilder superQuery, Func<IDataRecord, TResult> mapper = null, SqlBuffer? buffer = null)
        {
            return new SqlSet<TResult>(this, superQuery, mapper, buffer);
        }
        internal SqlSet<TResult> CreateSet<TResult>(string[] fromSelect, SqlBuffer? buffer = null)
        {
            return new SqlSet<TResult>(this, fromSelect, buffer);
        }
        internal SqlSet Clone()
        {
            return CreateBufferedSet(ignoreBuffer: true, buffer: this.buffer);
        }
        internal SqlSet CreateBufferedSet(bool ignoreBuffer, SqlBuffer buffer, Type resultType = null)
        {
            SqlSet set = null;
            if (ignoreBuffer
               && this.definingQuery == null)
            {
                set = CreateSet(this.fromSelect, resultType, buffer);
            }
            if (set == null)
            {
                SqlBuilder query = GetDefiningQuery(ignoreBuffer: ignoreBuffer);
                set = CreateSet(query, resultType, buffer);
            }
            return set;
        }
        internal SqlSet<TResult> CreateBufferedSet<TResult>(bool ignoreBuffer, SqlBuffer buffer)
        {
            SqlSet<TResult> set = null;
            if (ignoreBuffer
               && this.definingQuery == null)
            {
                set = CreateSet<TResult>(this.fromSelect, buffer);
            }
            if (set == null)
            {
                SqlBuilder query = GetDefiningQuery(ignoreBuffer: ignoreBuffer);
                set = CreateSet<TResult>(query, default(Func<IDataRecord, TResult>), buffer);
            }
            return set;
        }
        internal virtual IEnumerable Map(bool singleResult)
        {
            if (this.ResultType != null)
            {
#if DBEX_NO_POCO
            throw new InvalidOperationException("Cannot enumerate this set.");
#else
                return PocoMap(singleResult);
#endif
            }
#if DBEX_NO_DYN
         throw new InvalidOperationException("Cannot enumerate this set unless you specify a result type.");
#else
            return DynamicMap(singleResult);
#endif
        }
        public bool All(string predicate, params object[] parameters)
        {
            if (predicate == null) throw new ArgumentNullException(nameof(predicate));
            return !Any(String.Concat("NOT (", predicate, ")"), parameters);
        }
        public bool Any()
        {
            var query = new SqlBuilder()
               .SELECT("(CASE WHEN EXISTS ({0}) THEN 1 ELSE 0 END)", GetDefiningQuery(clone: false));
            return this.db.Map(query, r => Convert.ToInt32(r[0], CultureInfo.InvariantCulture) != 0)
               .SingleOrDefault();
        }
        public bool Any(string predicate, params object[] parameters)
        {
            return Where(predicate, parameters).Any();
        }
        public IEnumerable<object> AsEnumerable()
        {
            return AsEnumerable(singleResult: false);
        }
        IEnumerable<object> AsEnumerable(bool singleResult)
        {
            IEnumerable enumerable = Map(singleResult);
            return enumerable as IEnumerable<object>
               ?? enumerable.Cast<object>();
        }
        public SqlSet<TResult> Cast<TResult>()
        {
            if (this.ResultType != null
               && this.ResultType != typeof(TResult))
            {
                throw new InvalidOperationException("The specified type parameter is not valid for this instance.");
            }
            return CreateBufferedSet<TResult>(ignoreBuffer: true, buffer: this.buffer);
        }
        public SqlSet Cast(Type resultType)
        {
            if (this.ResultType != null
               && this.ResultType != resultType)
            {
                throw new InvalidOperationException("The specified resultType is not valid for this instance.");
            }
            return CreateBufferedSet(ignoreBuffer: true, buffer: this.buffer, resultType: resultType);
        }
        public int Count()
        {
            var query = new SqlBuilder()
               .SELECT("COUNT(*)")
               .FROM("({0}) dbex_count", GetDefiningQuery(clone: false));
            return this.db.Map(query, r => (int?)Convert.ToInt32(r[0], CultureInfo.InvariantCulture))
               .SingleOrDefault() ?? 0;
        }
        public int Count(string predicate, params object[] parameters)
        {
            return Where(predicate, parameters).Count();
        }
        public object First()
        {
            return Take(1).AsEnumerable(singleResult: true).First();
        }
        public object First(string predicate, params object[] parameters)
        {
            return Where(predicate, parameters).First();
        }
        public object FirstOrDefault()
        {
            return Take(1).AsEnumerable(singleResult: true).FirstOrDefault();
        }
        public object FirstOrDefault(string predicate, params object[] parameters)
        {
            return Where(predicate, parameters).FirstOrDefault();
        }
        public IEnumerator<object> GetEnumerator()
        {
            return AsEnumerable().GetEnumerator();
        }
        [SuppressMessage("Microsoft.Naming", "CA1720:IdentifiersShouldNotContainTypeNames", MessageId = "long", Justification = "Consistent with LINQ.")]
        public long LongCount()
        {
            var query = new SqlBuilder()
               .SELECT("COUNT(*)")
               .FROM("({0}) dbex_count", GetDefiningQuery(clone: false));
            return this.db.Map(query, r => (long?)Convert.ToInt64(r[0], CultureInfo.InvariantCulture))
               .SingleOrDefault() ?? 0L;
        }
        [SuppressMessage("Microsoft.Naming", "CA1720:IdentifiersShouldNotContainTypeNames", MessageId = "long", Justification = "Consistent with LINQ.")]
        public long LongCount(string predicate, params object[] parameters)
        {
            return Where(predicate, parameters).LongCount();
        }
        public SqlSet OrderBy(string columnList, params object[] parameters)
        {
            bool ignoreBuffer = this.buffer.OrderBy == null
               && this.buffer.Skip == null
               && this.buffer.Take == null;
            var newBuffer = new SqlBuffer(
               where: (ignoreBuffer) ? this.buffer.Where : null,
               orderBy: new SqlFragment(columnList, parameters),
               skip: null,
               take: null
            );
            SqlSet set = CreateBufferedSet(ignoreBuffer, newBuffer);
            return set;
        }
        public SqlSet<TResult> Select<TResult>(string columnList, params object[] parameters)
        {
            SqlBuilder query = GetDefiningQuery(selectFormat: columnList, args: parameters);
            return CreateSet<TResult>(query);
        }
        public SqlSet<TResult> Select<TResult>(Func<IDataRecord, TResult> mapper, string columnList, params object[] parameters)
        {
            SqlBuilder query = GetDefiningQuery(selectFormat: columnList, args: parameters);
            return CreateSet<TResult>(query, mapper);
        }
        public SqlSet Select(Type resultType, string columnList, params object[] parameters)
        {
            SqlBuilder query = GetDefiningQuery(selectFormat: columnList, args: parameters);
            return CreateSet(query, resultType);
        }
        public SqlSet Select(string columnList, params object[] parameters)
        {
            SqlBuilder query = GetDefiningQuery(selectFormat: columnList, args: parameters);
            return CreateSet(query);
        }
        public object Single()
        {
            return AsEnumerable(singleResult: true).Single();
        }
        public object Single(string predicate, params object[] parameters)
        {
            return Where(predicate, parameters).Single();
        }
        public object SingleOrDefault()
        {
            return AsEnumerable(singleResult: true).SingleOrDefault();
        }
        public object SingleOrDefault(string predicate, params object[] parameters)
        {
            return Where(predicate, parameters).SingleOrDefault();
        }
        public SqlSet Skip(int count)
        {
            bool ignoreBuffer = this.buffer.Skip == null
               && this.buffer.Take == null;
            var newBuffer = new SqlBuffer(
               where: (ignoreBuffer) ? this.buffer.Where : null,
               orderBy: (ignoreBuffer) ? this.buffer.OrderBy : null,
               skip: count,
               take: null
            );
            SqlSet set = CreateBufferedSet(ignoreBuffer, newBuffer);
            return set;
        }
        public SqlSet Take(int count)
        {
            bool ignoreBuffer = this.buffer.Take == null;
            var newBuffer = new SqlBuffer(
               where: (ignoreBuffer) ? this.buffer.Where : null,
               orderBy: (ignoreBuffer) ? this.buffer.OrderBy : null,
               skip: (ignoreBuffer) ? this.buffer.Skip : null,
               take: count
            );
            SqlSet set = CreateBufferedSet(ignoreBuffer, newBuffer);
            return set;
        }
        public object[] ToArray()
        {
            return AsEnumerable().ToArray();
        }
        [SuppressMessage("Microsoft.Design", "CA1002:DoNotExposeGenericLists", Justification = "Consistent with LINQ.")]
        public List<object> ToList()
        {
            return AsEnumerable().ToList();
        }
        public SqlSet Where(string predicate, params object[] parameters)
        {
            bool ignoreBuffer = this.buffer.Where == null
               && this.buffer.OrderBy == null
               && this.buffer.Skip == null
               && this.buffer.Take == null;
            var newBuffer = new SqlBuffer(
               where: new SqlFragment(predicate, parameters),
               orderBy: null,
               skip: null,
               take: null
            );
            SqlSet set = CreateBufferedSet(ignoreBuffer, newBuffer);
            return set;
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
        public override string ToString()
        {
            return GetDefiningQuery(clone: false).ToString();
        }
        internal struct SqlBuffer
        {
            public readonly SqlFragment Where;
            public readonly SqlFragment OrderBy;
            public readonly int? Skip;
            public readonly int? Take;
            public bool HasBuffer
            {
                get
                {
                    return Where != null
                       || OrderBy != null
                       || Skip != null
                       || Take != null;
                }
            }
            public SqlBuffer(SqlFragment where, SqlFragment orderBy, int? skip, int? take)
            {
                this.Where = where;
                this.OrderBy = orderBy;
                this.Skip = skip;
                this.Take = take;
            }
        }
        internal class SqlFragment
        {
            public readonly string Format;
            public readonly object[] Args;
            public SqlFragment(string format, object[] args)
            {
                this.Format = format;
                this.Args = args;
            }
        }
    }
    public partial class SqlSet<TResult> : SqlSet, ISqlSet<SqlSet<TResult>, TResult>
    {
        readonly Func<IDataRecord, TResult> explicitMapper;
        internal SqlSet(SqlBuilder definingQuery, Database db)
           : base(definingQuery, typeof(TResult), db) { }
        internal SqlSet(SqlBuilder definingQuery, Func<IDataRecord, TResult> mapper, Database db)
           : base(definingQuery, typeof(TResult), db)
        {
            if (mapper == null) throw new ArgumentNullException(nameof(mapper));
            this.explicitMapper = mapper;
        }
        internal SqlSet(string[] fromSelect, Database db)
           : base(fromSelect, typeof(TResult), db) { }
        private SqlSet(SqlSet<TResult> set, SqlBuilder superQuery, SqlBuffer? buffer)
           : base((SqlSet)set, superQuery, default(Type), buffer)
        {
            if (set == null) throw new ArgumentNullException(nameof(set));
            this.explicitMapper = set.explicitMapper;
        }
        private SqlSet(SqlSet<TResult> set, string[] fromSelect, SqlBuffer? buffer)
           : base((SqlSet)set, fromSelect, default(Type), buffer)
        {
            if (set == null) throw new ArgumentNullException(nameof(set));
            this.explicitMapper = set.explicitMapper;
        }
        internal SqlSet(SqlSet set, SqlBuilder superQuery, Func<IDataRecord, TResult> mapper, SqlBuffer? buffer)
           : base(set, superQuery, typeof(TResult), buffer)
        {
            if (mapper != null)
            {
                this.explicitMapper = mapper;
            }
        }
        internal SqlSet(SqlSet set, string[] fromSelect, SqlBuffer? buffer)
           : base(set, fromSelect, typeof(TResult), buffer) { }
        internal override SqlSet CreateSet(SqlBuilder superQuery, Type resultType = null, SqlBuffer? buffer = null)
        {
            if (resultType != null)
            {
                return base.CreateSet(superQuery, resultType, buffer);
            }
            return new SqlSet<TResult>(this, superQuery, buffer);
        }
        internal override SqlSet CreateSet(string[] fromSelect, Type resultType = null, SqlBuffer? buffer = null)
        {
            if (resultType != null)
            {
                return base.CreateSet(fromSelect, resultType, buffer);
            }
            return new SqlSet<TResult>(this, fromSelect, buffer);
        }
        internal override IEnumerable Map(bool singleResult)
        {
            if (this.explicitMapper != null)
            {
                SqlBuilder query = GetDefiningQuery(clone: false);
                return this.db.Map(query, this.explicitMapper);
            }
            return base.Map(singleResult).Cast<TResult>();
        }
        public new IEnumerable<TResult> AsEnumerable()
        {
            return AsEnumerable(singleResult: false);
        }
        IEnumerable<TResult> AsEnumerable(bool singleResult)
        {
            return (IEnumerable<TResult>)Map(singleResult);
        }
        [EditorBrowsable(EditorBrowsableState.Never)]
        public new SqlSet<T> Cast<T>()
        {
            return base.Cast<T>();
        }
        [EditorBrowsable(EditorBrowsableState.Never)]
        public new SqlSet Cast(Type resultType)
        {
            return base.Cast(resultType);
        }
        public new TResult First()
        {
            return Take(1).AsEnumerable(singleResult: true).First();
        }
        public new TResult First(string predicate, params object[] parameters)
        {
            return Where(predicate, parameters).First();
        }
        public new TResult FirstOrDefault()
        {
            return Take(1).AsEnumerable(singleResult: true).FirstOrDefault();
        }
        public new TResult FirstOrDefault(string predicate, params object[] parameters)
        {
            return Where(predicate, parameters).FirstOrDefault();
        }
        public new IEnumerator<TResult> GetEnumerator()
        {
            return AsEnumerable().GetEnumerator();
        }
        public new SqlSet<TResult> OrderBy(string columnList, params object[] parameters)
        {
            return (SqlSet<TResult>)base.OrderBy(columnList, parameters);
        }
        public new TResult Single()
        {
            return AsEnumerable(singleResult: true).Single();
        }
        public new TResult Single(string predicate, params object[] parameters)
        {
            return Where(predicate, parameters).Single();
        }
        public new TResult SingleOrDefault()
        {
            return AsEnumerable(singleResult: true).SingleOrDefault();
        }
        public new TResult SingleOrDefault(string predicate, params object[] parameters)
        {
            return Where(predicate, parameters).SingleOrDefault();
        }
        public new SqlSet<TResult> Skip(int count)
        {
            return (SqlSet<TResult>)base.Skip(count);
        }
        public new SqlSet<TResult> Take(int count)
        {
            return (SqlSet<TResult>)base.Take(count);
        }
        public new TResult[] ToArray()
        {
            return AsEnumerable().ToArray();
        }
        [SuppressMessage("Microsoft.Design", "CA1002:DoNotExposeGenericLists", Justification = "Consistent with LINQ.")]
        public new List<TResult> ToList()
        {
            return AsEnumerable().ToList();
        }
        public new SqlSet<TResult> Where(string predicate, params object[] parameters)
        {
            return (SqlSet<TResult>)base.Where(predicate, parameters);
        }
    }
    interface ISqlSet<TSqlSet, TSource> where TSqlSet : SqlSet
    {
        bool All(string predicate, params object[] parameters);
        bool Any();
        bool Any(string predicate, params object[] parameters);
        IEnumerable<TSource> AsEnumerable();
        SqlSet<TResult> Cast<TResult>();
        SqlSet Cast(Type resultType);
        int Count();
        int Count(string predicate, params object[] parameters);
        TSource First();
        TSource First(string predicate, params object[] parameters);
        TSource FirstOrDefault();
        TSource FirstOrDefault(string predicate, params object[] parameters);
        IEnumerator<TSource> GetEnumerator();
        long LongCount();
        long LongCount(string predicate, params object[] parameters);
        TSqlSet OrderBy(string columnList, params object[] parameters);
        SqlSet<TResult> Select<TResult>(string columnList, params object[] parameters);
        SqlSet<TResult> Select<TResult>(Func<IDataRecord, TResult> mapper, string columnList, params object[] parameters);
        SqlSet Select(string columnList, params object[] parameters);
        SqlSet Select(Type resultType, string columnList, params object[] parameters);
        TSource Single();
        TSource Single(string predicate, params object[] parameters);
        TSource SingleOrDefault();
        TSource SingleOrDefault(string predicate, params object[] parameters);
        TSqlSet Skip(int count);
        TSqlSet Take(int count);
        TSource[] ToArray();
        List<TSource> ToList();
        TSqlSet Where(string predicate, params object[] parameters);
    }
}