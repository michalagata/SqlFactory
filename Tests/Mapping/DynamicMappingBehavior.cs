using System;
using System.Linq;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AnubisWorks.SQLFactory.Tests.Mapping {

   using static TestUtil;

   [TestClass]
   public class DynamicMappingBehavior {

      readonly Database db = SqlServerDatabase();

      [TestMethod, ExpectedException(typeof(ArgumentException))]
      public void Constructor_Parameters_Not_Allowed() {

         var value = db.Map(SQL
            .SELECT("'foo' AS '1'"))
            .Single();
      }
   }
}
