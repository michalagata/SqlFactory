using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
namespace AnubisWorks.SQLFactory
{
    [CLSCompliant(true)]
    [DebuggerDisplay("{Buffer}")]
    public partial class SqlBuilder
    {
        bool? ifCondition;
        public StringBuilder Buffer { get; } = new StringBuilder();
        public Collection<object> ParameterValues { get; } = new Collection<object>();
        public string CurrentClause { get; set; }
        public string CurrentSeparator { get; set; }
        public string NextClause { get; set; }
        public string NextSeparator { get; set; }
        public bool IsEmpty => Buffer.Length == 0;
        public static SqlBuilder JoinSql(string separator, params SqlBuilder[] values)
        {
            if (values == null) throw new ArgumentNullException(nameof(values));
            var sql = new SqlBuilder();
            if (values.Length == 0)
            {
                return sql;
            }
            if (separator == null)
            {
                separator = "";
            }
            SqlBuilder first = values[0];
            if (first != null)
            {
                sql.Append(first);
            }
            for (int i = 1; i < values.Length; i++)
            {
                sql.Append(separator);
                SqlBuilder val = values[i];
                if (val != null)
                {
                    sql.Append(val);
                }
            }
            return sql;
        }
        public static SqlBuilder JoinSql(string separator, IEnumerable<SqlBuilder> values)
        {
            if (values == null) throw new ArgumentNullException(nameof(values));
            var sql = new SqlBuilder();
            if (separator == null)
            {
                separator = "";
            }
            using (IEnumerator<SqlBuilder> enumerator = values.GetEnumerator())
            {
                if (!enumerator.MoveNext())
                {
                    return sql;
                }
                if (enumerator.Current != null)
                {
                    sql.Append(enumerator.Current);
                }
                while (enumerator.MoveNext())
                {
                    sql.Append(separator);
                    if (enumerator.Current != null)
                    {
                        sql.Append(enumerator.Current);
                    }
                }
            }
            return sql;
        }
        public SqlBuilder() { }
        public SqlBuilder(string format, params object[] args)
        {
            Append(format, args);
        }
        public SqlBuilder AppendClause(string clauseName, string separator, string format, params object[] args)
        {
            if (separator == null
               || !String.Equals(clauseName, this.CurrentClause, StringComparison.OrdinalIgnoreCase))
            {
                if (!this.IsEmpty)
                {
                    this.Buffer.AppendLine();
                }
                if (clauseName != null)
                {
                    this.Buffer.Append(clauseName);
                    this.Buffer.Append(" ");
                }
            }
            else if (separator != null)
            {
                this.Buffer.Append(separator);
            }
            Append(format, args);
            this.CurrentClause = clauseName;
            this.CurrentSeparator = separator;
            this.NextClause = null;
            this.NextSeparator = null;
            this.ifCondition = null;
            return this;
        }
        public SqlBuilder AppendToCurrentClause(string format, params object[] args)
        {
            string clause = this.CurrentClause;
            string separator = this.CurrentSeparator;
            if (this.NextClause != null)
            {
                clause = this.NextClause;
                separator = this.NextSeparator;
            }
            AppendClause(clause, separator, format, args);
            return this;
        }
        public SqlBuilder Append(SqlBuilder sql)
        {
            this.Buffer.Append(MakeAbsolutePlaceholders(sql));
            for (int i = 0; i < sql.ParameterValues.Count; i++)
            {
                this.ParameterValues.Add(sql.ParameterValues[i]);
            }
            return this;
        }
        public SqlBuilder Append(string format, params object[] args)
        {
            if (args == null || args.Length == 0)
            {
                this.Buffer.Append(format);
                return this;
            }
            var fargs = new List<string>();
            for (int i = 0; i < args.Length; i++)
            {
                object obj = args[i];
                if (obj != null)
                {
                    SqlList list = obj as SqlList;
                    if (list != null)
                    {
                        fargs.Add(String.Join(", ", Enumerable.Range(0, list.Count).Select(x => Placeholder(this.ParameterValues.Count + x))));
                        for (int j = 0; j < list.Count; j++)
                        {
                            this.ParameterValues.Add(list[j]);
                        }
                        continue;
                    }
                    else
                    {
                        var sqlb = obj as SqlBuilder;
                        if (sqlb == null)
                        {
                            GetDefiningQueryFromObject(obj, ref sqlb);
                        }
                        if (sqlb != null)
                        {
                            var sqlfrag = new StringBuilder()
                               .AppendLine()
                               .Append(MakeAbsolutePlaceholders(sqlb))
                               .Replace(Environment.NewLine, Environment.NewLine + "\t");
                            fargs.Add(sqlfrag.ToString());
                            for (int j = 0; j < sqlb.ParameterValues.Count; j++)
                            {
                                this.ParameterValues.Add(sqlb.ParameterValues[j]);
                            }
                            continue;
                        }
                    }
                }
                fargs.Add(Placeholder(this.ParameterValues.Count));
                this.ParameterValues.Add(obj);
            }
            if (format == null)
            {
                format = String.Join(" ", Enumerable.Range(0, fargs.Count).Select(i => Placeholder(i)));
            }
            this.Buffer.AppendFormat(CultureInfo.InvariantCulture, format, fargs.Cast<object>().ToArray());
            return this;
        }
        partial void GetDefiningQueryFromObject(object obj, ref SqlBuilder definingQuery);
        string MakeAbsolutePlaceholders(SqlBuilder sql)
        {
            return String.Format(CultureInfo.InvariantCulture, sql.ToString(), Enumerable.Range(0, sql.ParameterValues.Count).Select(x => Placeholder(this.ParameterValues.Count + x)).ToArray());
        }
        static string Placeholder(int index)
        {
            return String.Concat("{", index.ToString(CultureInfo.InvariantCulture), "}");
        }
        public SqlBuilder AppendLine()
        {
            this.Buffer.AppendLine();
            return this;
        }
        public SqlBuilder Insert(int index, string value)
        {
            this.Buffer.Insert(index, value);
            return this;
        }
        public SqlBuilder SetCurrentClause(string clauseName, string separator)
        {
            this.CurrentClause = clauseName;
            this.CurrentSeparator = separator;
            return this;
        }
        public SqlBuilder SetNextClause(string clauseName, string separator)
        {
            this.NextClause = clauseName;
            this.NextSeparator = separator;
            this.ifCondition = null;
            return this;
        }
        public override string ToString()
        {
            return this.Buffer.ToString();
        }
        public SqlBuilder Clone()
        {
            var clone = new SqlBuilder();
            clone.Buffer.Append(this.Buffer.ToString());
            clone.CurrentClause = this.CurrentClause;
            clone.CurrentSeparator = this.CurrentSeparator;
            foreach (object item in this.ParameterValues)
            {
                clone.ParameterValues.Add(item);
            }
            return clone;
        }
        [CLSCompliant(false)]
        public SqlBuilder _(string format, params object[] args)
        {
            return AppendToCurrentClause(format, args);
        }
        [CLSCompliant(false)]
        public SqlBuilder _If(bool condition, string format, params object[] args)
        {
            if (condition)
            {
                _(format, args);
            }
            this.ifCondition = condition;
            return this;
        }
        [CLSCompliant(false)]
        public SqlBuilder _ElseIf(bool condition, string format, params object[] args)
        {
            if (this.ifCondition == false)
            {
                if (condition)
                {
                    _(format, args);
                }
                this.ifCondition = condition;
            }
            return this;
        }
        [CLSCompliant(false)]
        public SqlBuilder _Else(string format, params object[] args)
        {
            if (this.ifCondition == false)
            {
                _(format, args);
            }
            return this;
        }
        [CLSCompliant(false)]
        public SqlBuilder _ForEach<T>(IEnumerable<T> items, string format, string itemFormat, string separator, Func<T, object[]> parametersFactory)
        {
            if (items == null) throw new ArgumentNullException(nameof(items));
            if (itemFormat == null) throw new ArgumentNullException(nameof(itemFormat));
            if (separator == null) throw new ArgumentNullException(nameof(separator));
            string formatStart = "", formatEnd = "";
            if (format != null)
            {
                string[] formatSplit = format.Split(new[] { "{0}" }, StringSplitOptions.None);
                formatStart = formatSplit[0];
                formatEnd = formatSplit[1];
            }
            if (parametersFactory == null)
            {
                parametersFactory = (item) => null;
            }
            string currentSeparator = this.NextSeparator ?? this.CurrentSeparator;
            bool first = true;
            foreach (var item in items)
            {
                string tempate = itemFormat;
                if (first)
                {
                    first = false;
                    tempate = formatStart + tempate;
                }
                else
                {
                    this.CurrentSeparator = separator;
                }
                AppendToCurrentClause(tempate, parametersFactory(item));
            }
            if (!first)
            {
                Append(formatEnd);
                this.CurrentSeparator = currentSeparator;
            }
            return this;
        }
        [CLSCompliant(false)]
        public SqlBuilder _OR<T>(IEnumerable<T> items, string itemFormat, Func<T, object[]> parametersFactory)
        {
            return _ForEach(items, "({0})", itemFormat, " OR ", parametersFactory);
        }
        public SqlBuilder WITH(string format, params object[] args)
        {
            return AppendClause("WITH", null, format, args);
        }
        public SqlBuilder WITH(SqlBuilder subQuery, string alias)
        {
            return WITH(alias + " AS ({0})", subQuery);
        }
        public SqlBuilder SELECT()
        {
            return SetNextClause("SELECT", ", ");
        }
        public SqlBuilder SELECT(string format, params object[] args)
        {
            return AppendClause("SELECT", ", ", format, args);
        }
        public SqlBuilder FROM(string format, params object[] args)
        {
            return AppendClause("FROM", ", ", format, args);
        }
        public SqlBuilder FROM(SqlBuilder subQuery, string alias)
        {
            return FROM("({0}) " + alias, subQuery);
        }
        public SqlBuilder JOIN()
        {
            return SetNextClause("JOIN", null);
        }
        public SqlBuilder JOIN(string format, params object[] args)
        {
            return AppendClause("JOIN", null, format, args);
        }
        public SqlBuilder LEFT_JOIN(string format, params object[] args)
        {
            return AppendClause("LEFT JOIN", null, format, args);
        }
        public SqlBuilder RIGHT_JOIN(string format, params object[] args)
        {
            return AppendClause("RIGHT JOIN", null, format, args);
        }
        public SqlBuilder INNER_JOIN(string format, params object[] args)
        {
            return AppendClause("INNER JOIN", null, format, args);
        }
        public SqlBuilder CROSS_JOIN(string format, params object[] args)
        {
            return AppendClause("CROSS JOIN", null, format, args);
        }
        public SqlBuilder WHERE()
        {
            return SetNextClause("WHERE", " AND ");
        }
        public SqlBuilder WHERE(string format, params object[] args)
        {
            return AppendClause("WHERE", " AND ", format, args);
        }
        public SqlBuilder GROUP_BY()
        {
            return SetNextClause("GROUP BY", ", ");
        }
        public SqlBuilder GROUP_BY(string format, params object[] args)
        {
            return AppendClause("GROUP BY", ", ", format, args);
        }
        public SqlBuilder HAVING()
        {
            return SetNextClause("HAVING", " AND ");
        }
        public SqlBuilder HAVING(string format, params object[] args)
        {
            return AppendClause("HAVING", " AND ", format, args);
        }
        public SqlBuilder ORDER_BY()
        {
            return SetNextClause("ORDER BY", ", ");
        }
        public SqlBuilder ORDER_BY(string format, params object[] args)
        {
            return AppendClause("ORDER BY", ", ", format, args);
        }
        public SqlBuilder LIMIT()
        {
            return SetNextClause("LIMIT", null);
        }
        public SqlBuilder LIMIT(string format, params object[] args)
        {
            return AppendClause("LIMIT", null, format, args);
        }
        public SqlBuilder LIMIT(int maxRecords)
        {
            return LIMIT("{0}", maxRecords);
        }
        public SqlBuilder OFFSET()
        {
            return SetNextClause("OFFSET", null);
        }
        public SqlBuilder OFFSET(string format, params object[] args)
        {
            return AppendClause("OFFSET", null, format, args);
        }
        public SqlBuilder OFFSET(int startIndex)
        {
            return OFFSET("{0}", startIndex);
        }
        public SqlBuilder UNION()
        {
            return AppendClause("UNION", null, null, null);
        }
        public SqlBuilder INSERT_INTO(string format, params object[] args)
        {
            return AppendClause("INSERT INTO", null, format, args);
        }
        public SqlBuilder DELETE_FROM(string format, params object[] args)
        {
            return AppendClause("DELETE FROM", null, format, args);
        }
        public SqlBuilder UPDATE(string format, params object[] args)
        {
            return AppendClause("UPDATE", null, format, args);
        }
        public SqlBuilder SET(string format, params object[] args)
        {
            return AppendClause("SET", ", ", format, args);
        }
        public SqlBuilder VALUES(params object[] args)
        {
            if (args == null || args.Length == 0)
            {
                throw new ArgumentException("args cannot be empty", nameof(args));
            }
            return AppendClause("VALUES", null, "({0})", SQL.List(args));
        }
    }
    public static partial class SQL
    {
        public static SqlBuilder WITH(string format, params object[] args)
        {
            return new SqlBuilder().WITH(format, args);
        }
        public static SqlBuilder WITH(SqlBuilder subQuery, string alias)
        {
            return new SqlBuilder().WITH(subQuery, alias);
        }
        public static SqlBuilder SELECT(string format, params object[] args)
        {
            return new SqlBuilder().SELECT(format, args);
        }
        public static SqlBuilder INSERT_INTO(string format, params object[] args)
        {
            return new SqlBuilder().INSERT_INTO(format, args);
        }
        public static SqlBuilder UPDATE(string format, params object[] args)
        {
            return new SqlBuilder().UPDATE(format, args);
        }
        public static SqlBuilder DELETE_FROM(string format, params object[] args)
        {
            return new SqlBuilder().DELETE_FROM(format, args);
        }
        public static object List(IEnumerable values)
        {
            return new SqlList(values);
        }
        public static object List(params object[] values)
        {
            return new SqlList(values);
        }
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static new bool Equals(object objectA, object objectB)
        {
            return Object.Equals(objectA, objectB);
        }
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static new bool ReferenceEquals(object objectA, object objectB)
        {
            return Object.ReferenceEquals(objectA, objectB);
        }
    }
    class SqlList
    {
        object[] values;
        public object this[int index] => values[index];
        public int Count => values.Length;
        public SqlList(IEnumerable values)
        {
            object[] arr = values?.Cast<object>()
               .ToArray();
            if (arr == null
               || arr.Length == 0)
            {
                arr = new object[1] { null };
            }
            this.values = arr;
        }
    }
}