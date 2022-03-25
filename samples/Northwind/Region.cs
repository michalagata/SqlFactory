using System;
using System.Collections.ObjectModel;
using SQLFactory;

namespace AnubisWorks.SQLFactory.Sample.Northwind {

   [Table(Name = "Region")]
   public class Region {

      [Column(IsPrimaryKey = true)]
      public int RegionID { get; set; }

      [Column]
      public string RegionDescription { get; set; }

      [Association(OtherKey = nameof(Territory.RegionID))]
      public Collection<Territory> Territories { get; } = new Collection<Territory>();
   }
}