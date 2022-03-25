#region Based on code from .NET Framework
#endregion

using System;

namespace AnubisWorks.SQLFactory.Metadata {

   /// <summary>
   /// Shared rules governing the mapping system.
   /// </summary>

   static class MappingSystem {

      /// <summary>
      /// Return true if this is a clr type supported as an inheritance discriminator.
      /// </summary>
      /// <param name="type"></param>
      /// <returns></returns>

      internal static bool IsSupportedDiscriminatorType(Type type) {

         if (type.IsGenericType
            && type.GetGenericTypeDefinition() == typeof(Nullable<>)) {

            type = type.GetGenericArguments()[0];
         }

         switch (Type.GetTypeCode(type)) {
            case TypeCode.Byte:
            case TypeCode.SByte:
            case TypeCode.Int16:
            case TypeCode.Int32:
            case TypeCode.Int64:
            case TypeCode.UInt16:
            case TypeCode.UInt32:
            case TypeCode.UInt64:
            case TypeCode.Char:
            case TypeCode.String:
            case TypeCode.Boolean:
               return true;
         }

         return false;
      }

      /// <summary>
      /// Return true if this is a CLR type supported as an identity member.  Since identity
      /// management (caching) relies on key members being hashable, only types implementing
      /// GetHashCode are supported.  Also, the runtime relies on identity members being comparable,
      /// so only types implementing Equals are supported.
      /// </summary>

      internal static bool IsSupportedIdentityType(Type type) {

         if (type.IsGenericType
            && type.GetGenericTypeDefinition() == typeof(Nullable<>)) {

            type = type.GetGenericArguments()[0];
         }

         if (type == typeof(Guid)
            || type == typeof(DateTime)
            || type == typeof(DateTimeOffset)
            || type == typeof(TimeSpan)) {

            return true;
         }

         switch (Type.GetTypeCode(type)) {
            case TypeCode.Byte:
            case TypeCode.SByte:
            case TypeCode.Int16:
            case TypeCode.Int32:
            case TypeCode.Int64:
            case TypeCode.UInt16:
            case TypeCode.UInt32:
            case TypeCode.UInt64:
            case TypeCode.Char:
            case TypeCode.String:
            case TypeCode.Boolean:
            case TypeCode.Decimal:
            case TypeCode.Single:
            case TypeCode.Double:
               return true;
         }

         return false;
      }
   }
}