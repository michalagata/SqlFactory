using System;
using SQLFactory;

namespace AnubisWorks.SQLFactory.Sample.Northwind {

   [Table]
   public class CustomerCustomerDemo {

      [Column(IsPrimaryKey = true)]
      public string CustomerID { get; set; }

      [Column(IsPrimaryKey = true)]
      public string CustomerTypeID { get; set; }

      [Association(ThisKey = nameof(CustomerTypeID))]
      public CustomerDemographic CustomerDemographic { get; set; }

      [Association(ThisKey = nameof(CustomerID))]
      public Customer Customer { get; set; }
   }
}