using System;
using System.ComponentModel;
using AnubisWorks.SQLFactory;
using RealLifeExample.Tips;

namespace RealLifeExample
{
    [DBTable, Table(Name = "LeaseAgreement")]
    public class LeaseAgreement
    {
        [Column(IsDbGenerated = true, IsPrimaryKey = true)]
        public Int32 IdLeaseAgreement { get; set; }

        [Column(IsDbGenerated = false, IsPrimaryKey = false)]
        public Int32 EstateId { get; set; }

        [Column]
        public Int32? BuildingId { get; set; }

        [Column]
        public string TenantsName { get; set; }

        [Column]
        public string TenantsRegon { get; set; }

        [Column]
        public String LeaseUsabilityFunctionOther { get; set; }

        [Column]
        public decimal? LeaseArea { get; set; }

        [Column]
        public decimal? RateOfRent { get; set; }

        [Column]
        public decimal? RationalRent { get; set; }

        [Column]
        public Int32? ParkingPlaceCount { get; set; }

        [Column]
        public decimal? ParkingPlaceRateOfRent { get; set; }

        [Column]
        [Description("Id ze słownika")]
        public Int32? ExpensesChargedPerson { get; set; }

        [Column]
        public Int32? ExpensesShare { get; set; }

        [Column]
        public decimal? DiscountValue { get; set; }

        [Column]
        public String DiscountSettlementDescription { get; set; }

        [Column]
        public Int32? Currency { get; set; }

        [Column]
        public DateTime? LeaseDateTermination { get; set; }

        [Column]
        public bool? IsLeaseDateTermination { get; set; }

        [Column]
        public bool? IsTerminationDateConfirmed { get; set; }


        public LeaseAgreement()
        {
        }
    }
}
