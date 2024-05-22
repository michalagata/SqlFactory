using System;
using System.Data;
using System.Diagnostics.CodeAnalysis;
namespace AnubisWorks.SQLFactory
{
    public static partial class SQLFactory
    {
        public static Boolean GetBoolean(this IDataRecord record, string name)
        {
            return record.GetBoolean(record.GetOrdinal(name));
        }
        public static Byte GetByte(this IDataRecord record, string name)
        {
            return record.GetByte(record.GetOrdinal(name));
        }
        public static Char GetChar(this IDataRecord record, string name)
        {
            return record.GetChar(record.GetOrdinal(name));
        }
        public static DateTime GetDateTime(this IDataRecord record, string name)
        {
            return record.GetDateTime(record.GetOrdinal(name));
        }
        public static Decimal GetDecimal(this IDataRecord record, string name)
        {
            return record.GetDecimal(record.GetOrdinal(name));
        }
        public static Double GetDouble(this IDataRecord record, string name)
        {
            return record.GetDouble(record.GetOrdinal(name));
        }
        public static Single GetFloat(this IDataRecord record, string name)
        {
            return record.GetFloat(record.GetOrdinal(name));
        }
        public static Int16 GetInt16(this IDataRecord record, string name)
        {
            return record.GetInt16(record.GetOrdinal(name));
        }
        public static Int32 GetInt32(this IDataRecord record, string name)
        {
            return record.GetInt32(record.GetOrdinal(name));
        }
        public static Int64 GetInt64(this IDataRecord record, string name)
        {
            return record.GetInt64(record.GetOrdinal(name));
        }
        public static String GetString(this IDataRecord record, string name)
        {
            return record.GetString(record.GetOrdinal(name));
        }
        public static Object GetValue(this IDataRecord record, string name)
        {
            return record.GetValue(record.GetOrdinal(name));
        }
        public static Boolean? GetNullableBoolean(this IDataRecord record, string name)
        {
            return GetNullableBoolean(record, record.GetOrdinal(name));
        }
        [SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId = "i", Justification = "Consistent with .NET Framework.")]
        public static Boolean? GetNullableBoolean(this IDataRecord record, int i)
        {
            return (record.IsDBNull(i)) ? default(Boolean?) : record.GetBoolean(i);
        }
        public static Byte? GetNullableByte(this IDataRecord record, string name)
        {
            return GetNullableByte(record, record.GetOrdinal(name));
        }
        [SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId = "i", Justification = "Consistent with .NET Framework.")]
        public static Byte? GetNullableByte(this IDataRecord record, int i)
        {
            return (record.IsDBNull(i)) ? default(Byte?) : record.GetByte(i);
        }
        public static Char? GetNullableChar(this IDataRecord record, string name)
        {
            return GetNullableChar(record, record.GetOrdinal(name));
        }
        [SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId = "i", Justification = "Consistent with .NET Framework.")]
        public static Char? GetNullableChar(this IDataRecord record, int i)
        {
            return (record.IsDBNull(i)) ? default(Char?) : record.GetChar(i);
        }
        public static DateTime? GetNullableDateTime(this IDataRecord record, string name)
        {
            return GetNullableDateTime(record, record.GetOrdinal(name));
        }
        [SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId = "i", Justification = "Consistent with .NET Framework.")]
        public static DateTime? GetNullableDateTime(this IDataRecord record, int i)
        {
            return (record.IsDBNull(i)) ? default(DateTime?) : record.GetDateTime(i);
        }
        public static Decimal? GetNullableDecimal(this IDataRecord record, string name)
        {
            return GetNullableDecimal(record, record.GetOrdinal(name));
        }
        [SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId = "i", Justification = "Consistent with .NET Framework.")]
        public static Decimal? GetNullableDecimal(this IDataRecord record, int i)
        {
            return (record.IsDBNull(i)) ? default(Decimal?) : record.GetDecimal(i);
        }
        public static Double? GetNullableDouble(this IDataRecord record, string name)
        {
            return GetNullableDouble(record, record.GetOrdinal(name));
        }
        [SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId = "i", Justification = "Consistent with .NET Framework.")]
        public static Double? GetNullableDouble(this IDataRecord record, int i)
        {
            return (record.IsDBNull(i)) ? default(Double?) : record.GetDouble(i);
        }
        public static Single? GetNullableFloat(this IDataRecord record, string name)
        {
            return GetNullableFloat(record, record.GetOrdinal(name));
        }
        [SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId = "i", Justification = "Consistent with .NET Framework.")]
        public static Single? GetNullableFloat(this IDataRecord record, int i)
        {
            return (record.IsDBNull(i)) ? default(Single?) : record.GetFloat(i);
        }
        public static Guid? GetNullableGuid(this IDataRecord record, string name)
        {
            return GetNullableGuid(record, record.GetOrdinal(name));
        }
        [SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId = "i", Justification = "Consistent with .NET Framework.")]
        public static Guid? GetNullableGuid(this IDataRecord record, int i)
        {
            return (record.IsDBNull(i)) ? default(Guid?) : record.GetGuid(i);
        }
        public static Int16? GetNullableInt16(this IDataRecord record, string name)
        {
            return GetNullableInt16(record, record.GetOrdinal(name));
        }
        [SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId = "i", Justification = "Consistent with .NET Framework.")]
        public static Int16? GetNullableInt16(this IDataRecord record, int i)
        {
            return (record.IsDBNull(i)) ? default(Int16?) : record.GetInt16(i);
        }
        public static Int32? GetNullableInt32(this IDataRecord record, string name)
        {
            return GetNullableInt32(record, record.GetOrdinal(name));
        }
        [SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId = "i", Justification = "Consistent with .NET Framework.")]
        public static Int32? GetNullableInt32(this IDataRecord record, int i)
        {
            return (record.IsDBNull(i)) ? default(Int32?) : record.GetInt32(i);
        }
        public static Int64? GetNullableInt64(this IDataRecord record, string name)
        {
            return GetNullableInt64(record, record.GetOrdinal(name));
        }
        [SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId = "i", Justification = "Consistent with .NET Framework.")]
        public static Int64? GetNullableInt64(this IDataRecord record, int i)
        {
            return (record.IsDBNull(i)) ? default(Int64?) : record.GetInt64(i);
        }
        public static String GetStringOrNull(this IDataRecord record, string name)
        {
            return GetStringOrNull(record, record.GetOrdinal(name));
        }
        [SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId = "i", Justification = "Consistent with .NET Framework.")]
        public static String GetStringOrNull(this IDataRecord record, int i)
        {
            return (record.IsDBNull(i)) ? default(String) : record.GetString(i);
        }
        public static Object GetValueOrNull(this IDataRecord record, string name)
        {
            return GetValueOrNull(record, record.GetOrdinal(name));
        }
        [SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId = "i", Justification = "Consistent with .NET Framework.")]
        public static Object GetValueOrNull(this IDataRecord record, int i)
        {
            return (record.IsDBNull(i)) ? null : record.GetValue(i);
        }
    }
}