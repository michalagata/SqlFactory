using System.Collections.ObjectModel;

namespace AnubisWorks.SQLFactory.Sample.Northwind {

   [Table(Name = "Territories")]
   public class Territory {

      [Column(IsPrimaryKey = true)]
      public string TerritoryID { get; set; }

      [Column]
      public string TerritoryDescription { get; set; }

      [Column]
      public int RegionID { get; set; }

      [Association(OtherKey = nameof(EmployeeTerritory.TerritoryID))]
      public Collection<EmployeeTerritory> EmployeeTerritories { get; } = new Collection<EmployeeTerritory>();

      [Association(ThisKey = nameof(RegionID))]
      public Region Region { get; set; }
   }
}
