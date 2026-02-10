CREATE TABLE [dbo].[LeaseAgreement](
	[IdLeaseAgreement] [int] IDENTITY(1,1) NOT NULL,
	[EstateId] [int] NOT NULL,
	[BuildingId] [int] NULL,
	[TenantsName] [nvarchar](100) NULL,
	[TenantsRegon] [nvarchar](20) NULL,
	[LeaseUsabilityFunctionOther] [nvarchar](50) NULL,
	[LeaseArea] [decimal](18, 2) NULL,
	[RateOfRent] [decimal](18, 2) NULL,
	[RationalRent] [decimal](18, 2) NULL,
	[ParkingPlaceCount] [int] NULL,
	[ParkingPlaceRateOfRent] [decimal](18, 2) NULL,
	[ExpensesChargedPerson] [int] NULL,
	[ExpensesShare] [int] NULL,
	[DiscountValue] [decimal](18, 2) NULL,
	[DiscountSettlementDescription] [nvarchar](50) NULL,
	[Currency] [int] NULL,
	[LeaseDateTermination] [datetime2](7) NULL,
	[IsLeaseDateTermination] [bit] NULL,
	[IsTerminationDateConfirmed] [bit] NULL,
	[Deleted] [bit] NOT NULL,
	[ModUser] [nvarchar](50) NULL,
	[TIME_STAMP] [datetime2](7) NOT NULL,
 CONSTRAINT [PK_LeaseAgreement_IdLeaseAgreement] PRIMARY KEY CLUSTERED 
(
	[IdLeaseAgreement] ASC
)
)

GO