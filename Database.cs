using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Transactions;
using IsolationLevel = System.Data.IsolationLevel;

namespace AnubisWorks.SQLFactory
{
    public partial class Database : IDisposable
    {
        static readonly ConcurrentDictionary<string, DbProviderFactory> factories = new ConcurrentDictionary<string, DbProviderFactory>();
        readonly bool disposeConnection;
        public IDbConnection Connection { get; }
        public IDbTransaction Transaction { get; set; }
        public DatabaseConfiguration Configuration { get; private set; }
        public Database()
        {
            string providerInvariantName;
            this.Connection = CreateConnection(null, null, out providerInvariantName);
            this.disposeConnection = true;
            Initialize(providerInvariantName);
        }

        public Database(string connectionString)
        {
            if (connectionString == null) throw new ArgumentNullException(nameof(connectionString));
            string providerInvariantName;
            this.Connection = CreateConnection(connectionString, null, out providerInvariantName);
            this.disposeConnection = true;
            Initialize(providerInvariantName);
        }

        public Database(string connectionString, string providerInvariantName)
        {
            if (connectionString == null) throw new ArgumentNullException(nameof(connectionString));
            string finalProviderInvariantName;
            this.Connection = CreateConnection(connectionString, providerInvariantName, out finalProviderInvariantName);
            this.disposeConnection = true;
            Initialize(finalProviderInvariantName);
        }

        public Database(IDbConnection connection)
        {
            if (connection == null) throw new ArgumentNullException(nameof(connection));
            this.Connection = connection;
            Initialize(null);
        }

        internal Database(IDbConnection connection, string providerInvariantName)
        {
            if (connection == null) throw new ArgumentNullException(nameof(connection));
            this.Connection = connection;
            Initialize(providerInvariantName);
        }

        void Initialize(string providerInvariantName)
        {
            providerInvariantName = providerInvariantName
               ?? this.Connection.GetType().Namespace;
            this.Configuration = new DatabaseConfiguration(
               providerInvariantName
               , () => CreateCommandBuilder(providerInvariantName)
            );
            Initialize2(providerInvariantName);
        }

        partial void Initialize2(string providerInvariantName);

        static IDbConnection CreateConnection(string connectionString, string callerProviderInvariantName, out string providerInvariantName)
        {
            connectionString = connectionString ?? DatabaseConfiguration.DefaultConnectionString;
            providerInvariantName = callerProviderInvariantName ?? DatabaseConfiguration.DefaultProviderInvariantName;
            if (connectionString == null)
            {
                throw new InvalidOperationException($"A default connection string name must be specified in the {typeof(DatabaseConfiguration).FullName}.{nameof(DatabaseConfiguration.DefaultConnectionString)} property.");
            }
            if (providerInvariantName == null)
            {
                throw new InvalidOperationException($"A default provider name must be specified in the {typeof(DatabaseConfiguration).FullName}.{nameof(DatabaseConfiguration.DefaultProviderInvariantName)} property.");
            }
            DbProviderFactory factory = GetProviderFactory(providerInvariantName);
            IDbConnection connection = factory.CreateConnection();
            connection.ConnectionString = connectionString;
            return connection;
        }
        static DbProviderFactory GetProviderFactory(string providerInvariantName)
        {
            if (providerInvariantName == null) throw new ArgumentNullException(nameof(providerInvariantName));
            DbProviderFactory factory = factories.GetOrAdd(providerInvariantName, n => DbProviderFactories.GetFactory(n));
            return factory;
        }
        DbCommandBuilder CreateCommandBuilder(string providerInvariantName)
        {
            DbConnection dbConn = this.Connection as DbConnection;
            DbProviderFactory factory = ((dbConn != null) ? DbProviderFactories.GetFactory(dbConn) : null)
               ?? GetProviderFactory(providerInvariantName);
            return factory.CreateCommandBuilder();
        }
        public IDisposable EnsureConnectionOpen()
        {
            return new ConnectionHolder(this.Connection);
        }
        public IDbTransaction EnsureInTransaction()
        {
            return EnsureInTransaction(IsolationLevel.Unspecified);
        }
        public IDbTransaction EnsureInTransaction(IsolationLevel isolationLevel)
        {
            return new WrappedTransaction(this, isolationLevel);
        }
        public int Execute(SqlBuilder nonQuery, int affect = -1, bool exact = false)
        {
            if (nonQuery == null) throw new ArgumentNullException(nameof(nonQuery));
            IDbCommand command = CreateCommand(nonQuery);
            using (EnsureConnectionOpen())
            {
                using (var tx = (affect > -1 ? EnsureInTransaction() : null))
                {
                    int affectedRecords;
                    try
                    {
                        affectedRecords = command.ExecuteNonQuery();
                    }
                    catch
                    {
                        Trace(command, error: true);
                        throw;
                    }
                    Trace(command, affectedRecords);
                    if (tx != null
                       && affectedRecords != affect)
                    {
                        string errorMessage = null;
                        if (exact)
                        {
                            errorMessage = String.Format(CultureInfo.InvariantCulture, "The number of affected records should be {0}, the actual number is {1}.", affect, affectedRecords);
                        }
                        else if (affectedRecords > affect)
                        {
                            errorMessage = String.Format(CultureInfo.InvariantCulture, "The number of affected records should be {0} or lower, the actual number is {1}.", affect, affectedRecords);
                        }
                        if (errorMessage != null)
                        {
                            throw new ChangeConflictException(errorMessage);
                        }
                    }
                    tx?.Commit();
                    return affectedRecords;
                }
            }
        }
        public int Execute(string commandText, params object[] parameters)
        {
            return Execute(new SqlBuilder(commandText, parameters));
        }
        public IEnumerable<TResult> Map<TResult>(SqlBuilder query, Func<IDataRecord, TResult> mapper)
        {
            return new MappingEnumerable<TResult>(CreateCommand(query), mapper, this.Configuration.Log);
        }
        [SuppressMessage("Microsoft.Design", "CA1024:UsePropertiesWhereAppropriate", Justification = "Operation is expensive.")]
        public virtual object LastInsertId()
        {
            if (String.IsNullOrEmpty(this.Configuration.LastInsertIdCommand))
            {
                throw new InvalidOperationException("Configuration.LastInsertIdCommand cannot be null.");
            }
            IDbCommand command = CreateCommand(this.Configuration.LastInsertIdCommand);
            object value = command.ExecuteScalar();
            Trace(command);
            return value;
        }
        public IDbCommand CreateCommand(SqlBuilder sqlBuilder)
        {
            if (sqlBuilder == null) throw new ArgumentNullException(nameof(sqlBuilder));
            return CreateCommand(sqlBuilder.ToString(), sqlBuilder.ParameterValues.ToArray());
        }
        [SuppressMessage("Microsoft.Security", "CA2100:Review SQL queries for security vulnerabilities")]
        public virtual IDbCommand CreateCommand(string commandText, params object[] parameters)
        {
            if (commandText == null) throw new ArgumentNullException(nameof(commandText));
            IDbCommand command = this.Connection.CreateCommand();
            IDbTransaction transaction = this.Transaction;
            if (transaction != null)
            {
                command.Transaction = transaction;
            }
            int commandTimeout = this.Configuration.CommandTimeout;
            if (commandTimeout > -1)
            {
                command.CommandTimeout = commandTimeout;
            }
            if (parameters == null || parameters.Length == 0)
            {
                command.CommandText = commandText;
                return command;
            }
            object[] paramPlaceholders = new object[parameters.Length];
            for (int i = 0; i < paramPlaceholders.Length; i++)
            {
                object paramValue = parameters[i];
                IDataParameter dbParam = paramValue as IDataParameter;
                if (dbParam == null)
                {
                    dbParam = command.CreateParameter();
                    dbParam.Value = paramValue ?? DBNull.Value;
                }
                dbParam.ParameterName = this.Configuration.ParameterNameBuilder("p" + i.ToString(CultureInfo.InvariantCulture));
                command.Parameters.Add(dbParam);
                paramPlaceholders[i] = this.Configuration.ParameterPlaceholderBuilder(dbParam.ParameterName);
            }
            command.CommandText = String.Format(CultureInfo.InvariantCulture, commandText, paramPlaceholders);
            return command;
        }
        public virtual string QuoteIdentifier(string unquotedIdentifier)
        {
            if (unquotedIdentifier == null) throw new ArgumentNullException(nameof(unquotedIdentifier));
            if (IsQuotedIdentifier(unquotedIdentifier))
            {
                return unquotedIdentifier;
            }
            string quotePrefix = this.Configuration.QuotePrefix;
            string quoteSuffix = this.Configuration.QuoteSuffix;
            var sb = new StringBuilder();
            if (!String.IsNullOrEmpty(quotePrefix))
            {
                sb.Append(quotePrefix);
            }
            if (!String.IsNullOrEmpty(quoteSuffix))
            {
                sb.Append(unquotedIdentifier.Replace(quoteSuffix, quoteSuffix + quoteSuffix));
                sb.Append(quoteSuffix);
            }
            else
            {
                sb.Append(unquotedIdentifier);
            }
            return sb.ToString();
        }
        bool IsQuotedIdentifier(string identifier)
        {
            if (identifier == null) throw new ArgumentNullException(nameof(identifier));
            string quotePrefix = this.Configuration.QuotePrefix;
            string quoteSuffix = this.Configuration.QuoteSuffix;
            if (identifier.Length < (quotePrefix?.Length + quoteSuffix?.Length))
            {
                return false;
            }
            return (!String.IsNullOrEmpty(quotePrefix) && identifier.StartsWith(quotePrefix, StringComparison.Ordinal))
                && (!String.IsNullOrEmpty(quoteSuffix) && identifier.EndsWith(quoteSuffix, StringComparison.Ordinal));
        }
        internal void Trace(IDbCommand command, int? affectedRecords = null, bool error = false)
        {
            Trace(command, this.Configuration.Log, affectedRecords, error);
        }
        internal static void Trace(IDbCommand command, TextWriter log, int? affectedRecords = null, bool error = false)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));
            if (log != null)
            {
                log.WriteLine();
                if (error)
                {
                    log.WriteLine("-- ERROR: The following command produced an error");
                }
                log.WriteLine(command.CommandText);
                for (int i = 0; i < command.Parameters.Count; i++)
                {
                    IDbDataParameter param = command.Parameters[i] as IDbDataParameter;
                    if (param != null)
                    {
                        log.WriteLine("-- {0}: {1} {2} (Size = {3}) [{4}]", param.ParameterName, param.Direction, param.DbType, param.Size, param.Value);
                    }
                }
                if (affectedRecords != null)
                {
                    log.WriteLine("-- [{0}] records affected.", affectedRecords.Value);
                }
            }
        }
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (this.disposeConnection)
                {
                    this.Connection?.Dispose();
                }
            }
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
        class ConnectionHolder : IDisposable
        {
            readonly IDbConnection conn;
            readonly bool prevStateWasClosed;
            public ConnectionHolder(IDbConnection conn)
            {
                if (conn == null) throw new ArgumentNullException(nameof(conn));
                this.conn = conn;
                this.prevStateWasClosed = (conn.State == ConnectionState.Closed);
                if (this.prevStateWasClosed)
                {
                    this.conn.Open();
                }
            }
            public void Dispose()
            {
                if (conn != null
                   && prevStateWasClosed
                   && conn.State != ConnectionState.Closed)
                {
                    conn.Close();
                }
            }
        }
        class WrappedTransaction : IDbTransaction
        {
            readonly Database db;
            readonly IDisposable connHolder;
            readonly IDbTransaction txAdo;
            readonly bool txBeganHere;
            readonly TransactionScope txScope;
            public IDbConnection Connection { get; }
            public IsolationLevel IsolationLevel { get; }
            public WrappedTransaction(Database db, IsolationLevel isolationLevel)
            {
                if (db == null) throw new ArgumentNullException(nameof(db));
                this.db = db;
                this.txAdo = this.db.Transaction;
                this.Connection = this.db.Connection;
                this.IsolationLevel = isolationLevel;
                this.connHolder = this.db.EnsureConnectionOpen();
                try
                {
                    if (System.Transactions.Transaction.Current != null)
                    {
                        this.txScope = new TransactionScope();
                    }
                    if (this.txScope == null
                       && this.txAdo == null)
                    {
                        this.db.Transaction = this.db.Connection.BeginTransaction(isolationLevel);
                        this.txAdo = this.db.Transaction;
                        this.db.Configuration.Log?.WriteLine("-- TRANSACTION STARTED");
                        this.txBeganHere = true;
                    }
                }
                catch
                {
                    this.connHolder.Dispose();
                    throw;
                }
            }
            public void Commit()
            {
                if (this.txScope != null)
                {
                    this.txScope.Complete();
                    return;
                }
                if (this.txBeganHere)
                {
                    try
                    {
                        this.txAdo.Commit();
                        this.db.Configuration.Log?.WriteLine("-- TRANSACTION COMMITED");
                    }
                    finally
                    {
                        RemoveTxFromDatabase();
                    }
                }
            }
            public void Rollback()
            {
                if (this.txScope != null)
                {
                    return;
                }
                if (this.txBeganHere)
                {
                    try
                    {
                        this.txAdo.Rollback();
                        this.db.Configuration.Log?.WriteLine("-- TRANSACTION ROLLED BACK");
                    }
                    finally
                    {
                        RemoveTxFromDatabase();
                    }
                }
                else
                {
                    throw new InvalidOperationException();
                }
            }
            public void Dispose()
            {
                try
                {
                    if (this.txScope != null)
                    {
                        this.txScope.Dispose();
                        return;
                    }
                    if (this.txBeganHere)
                    {
                        try
                        {
                            this.txAdo.Dispose();
                        }
                        finally
                        {
                            RemoveTxFromDatabase();
                        }
                    }
                }
                finally
                {
                    this.connHolder.Dispose();
                }
            }
            void RemoveTxFromDatabase()
            {
                if (this.db.Transaction != null
                   && Object.ReferenceEquals(this.db.Transaction, this.txAdo))
                {
                    this.db.Transaction = null;
                }
            }
        }
    }
    public sealed partial class DatabaseConfiguration
    {
        static readonly Func<DbCommandBuilder, int, string> getParameterNameI =
           (Func<DbCommandBuilder, int, string>)Delegate.CreateDelegate(typeof(Func<DbCommandBuilder, int, string>), typeof(DbCommandBuilder).GetMethod("GetParameterName", BindingFlags.Instance | BindingFlags.NonPublic, Type.DefaultBinder, new[] { typeof(int) }, null));
        static readonly Func<DbCommandBuilder, string, string> getParameterNameS =
           (Func<DbCommandBuilder, string, string>)Delegate.CreateDelegate(typeof(Func<DbCommandBuilder, string, string>), typeof(DbCommandBuilder).GetMethod("GetParameterName", BindingFlags.Instance | BindingFlags.NonPublic, Type.DefaultBinder, new[] { typeof(string) }, null));
        static readonly Func<DbCommandBuilder, int, string> getParameterPlaceholder =
           (Func<DbCommandBuilder, int, string>)Delegate.CreateDelegate(typeof(Func<DbCommandBuilder, int, string>), typeof(DbCommandBuilder).GetMethod("GetParameterPlaceholder", BindingFlags.Instance | BindingFlags.NonPublic, Type.DefaultBinder, new[] { typeof(int) }, null));
        public static string DefaultConnectionString { get; set; }
        public static string DefaultProviderInvariantName { get; set; }
        public string QuotePrefix { get; set; } = "[";
        public string QuoteSuffix { get; set; } = "]";
        public Func<string, string> ParameterNameBuilder { get; set; } = (name) => "@" + name;
        public Func<string, string> ParameterPlaceholderBuilder { get; set; } = (paramName) => paramName;
        public string LastInsertIdCommand { get; set; } = "SELECT @@identity";
        public TextWriter Log { get; set; }
        public int CommandTimeout { get; set; } = -1;
        internal SqlDialect SqlDialect { get; set; }
        internal DatabaseConfiguration(string providerInvariantName, Func<DbCommandBuilder> cbFn = null)
        {
            if (providerInvariantName == null) throw new ArgumentNullException(nameof(providerInvariantName));
            switch (providerInvariantName)
            {
                case "System.Data.SqlClient":
                    this.SqlDialect = SqlDialect.TSql;
                    break;
                case "MySql.Data.MySqlClient":
                    this.QuotePrefix = "`";
                    this.QuoteSuffix = this.QuotePrefix;
                    break;
                case "System.Data.Odbc":
                case "System.Data.OleDb":
                    this.ParameterNameBuilder = (name) => name;
                    this.ParameterPlaceholderBuilder = (paramName) => "?";
                    break;
                case "System.Data.SQLite":
                    this.LastInsertIdCommand = "SELECT LAST_INSERT_ROWID()";
                    break;
                default:
                    if (providerInvariantName == "System.Data.SqlServerCe"
                       || providerInvariantName.StartsWith("System.Data.SqlServerCe."))
                    {
                        this.SqlDialect = SqlDialect.TSql;
                    }
                    else
                    {
                        DbCommandBuilder cb = cbFn?.Invoke();
                        if (cb != null)
                        {
                            Initialize(cb);
                        }
                    }
                    break;
            }
        }
        void Initialize(DbCommandBuilder cb)
        {
            string qp = cb.QuotePrefix;
            string qs = cb.QuoteSuffix;
            if (!String.IsNullOrEmpty(qp)
               || !String.IsNullOrEmpty(qs))
            {
                this.QuotePrefix = qp;
                this.QuoteSuffix = qs;
            }
            this.ParameterNameBuilder = (name) => getParameterNameS(cb, name);
            string pName = getParameterNameI(cb, 1);
            string pPlace = getParameterPlaceholder(cb, 1);
            if (!(Object.ReferenceEquals(pName, pPlace)
               || pName == pPlace))
            {
                this.ParameterPlaceholderBuilder = (paramName) => pPlace.Replace(pName, paramName);
            }
        }
    }
    enum SqlDialect
    {
        Default = 0,
        TSql
    }
    [Serializable]
    public class ChangeConflictException : Exception
    {
        public ChangeConflictException(string message)
           : base(message) { }
    }
    class MappingEnumerable<TResult> : IEnumerable<TResult>, IEnumerable, IDisposable
    {
        IEnumerator<TResult> enumerator;
        public MappingEnumerable(IDbCommand command, Func<IDataRecord, TResult> mapper, TextWriter logger = null)
        {
            this.enumerator = new MappingEnumerable<TResult>.Enumerator(command, mapper, logger);
        }
        public IEnumerator<TResult> GetEnumerator()
        {
            IEnumerator<TResult> e = this.enumerator;
            if (e == null)
            {
                throw new InvalidOperationException("Cannot enumerate more than once.");
            }
            this.enumerator = null;
            return e;
        }
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
        public void Dispose()
        {
            this.enumerator?.Dispose();
        }
        class Enumerator : IEnumerator<TResult>, IEnumerator, IDisposable
        {
            readonly IDbCommand command;
            readonly Func<IDataRecord, TResult> mapper;
            readonly TextWriter logger;
            readonly bool prevStateWasClosed;
            IDataReader reader;
            public Enumerator(IDbCommand command, Func<IDataRecord, TResult> mapper, TextWriter logger)
            {
                if (command == null) throw new ArgumentNullException(nameof(command));
                if (mapper == null) throw new ArgumentNullException(nameof(mapper));
                IDbConnection conn = command.Connection;
                if (conn == null)
                {
                    throw new ArgumentException("command.Connection cannot be null", nameof(command));
                }
                prevStateWasClosed = (conn.State == ConnectionState.Closed);
                this.command = command;
                this.mapper = mapper;
                this.logger = logger;
            }
            public TResult Current { get; private set; }
            object IEnumerator.Current => Current;
            public bool MoveNext()
            {
                if (this.reader == null)
                {
                    PossiblyOpenConnection();
                    try
                    {
                        this.reader = this.command.ExecuteReader();
                        Database.Trace(this.command, this.logger, this.reader.RecordsAffected);
                    }
                    catch
                    {
                        try
                        {
                            Database.Trace(this.command, this.logger, error: true);
                        }
                        finally
                        {
                            PossiblyCloseConnection();
                        }
                        throw;
                    }
                }
                if (this.reader.IsClosed)
                {
                    return false;
                }
                try
                {
                    if (this.reader.Read())
                    {
                        this.Current = this.mapper(this.reader);
                        return true;
                    }
                }
                catch
                {
                    PossiblyCloseConnection();
                    throw;
                }
                PossiblyCloseConnection();
                return false;
            }
            public void Reset()
            {
                throw new NotSupportedException();
            }
            public void Dispose()
            {
                this.reader?.Dispose();
                PossiblyCloseConnection();
            }
            void PossiblyOpenConnection()
            {
                if (this.prevStateWasClosed)
                {
                    this.command.Connection.Open();
                }
            }
            void PossiblyCloseConnection()
            {
                if (this.prevStateWasClosed)
                {
                    IDbConnection conn = this.command.Connection;
                    if (conn.State != ConnectionState.Closed)
                    {
                        conn.Close();
                    }
                }
            }
        }
    }
}