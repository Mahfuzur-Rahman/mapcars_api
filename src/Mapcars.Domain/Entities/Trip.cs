using Mapcars.Domain.Common;
using Mapcars.Domain.Enums;

namespace Mapcars.Domain.Entities;

public class Trip : BaseEntity
{
    public Guid RiderId { get; set; }
    public Rider? Rider { get; set; }

    public Guid? DriverId { get; set; }
    public Driver? Driver { get; set; }

    public required string PickupAddress { get; set; }
    public double PickupLat { get; set; }
    public double PickupLng { get; set; }

    public required string DropoffAddress { get; set; }
    public double DropoffLat { get; set; }
    public double DropoffLng { get; set; }

    public TripStatus Status { get; set; } = TripStatus.Requested;

    /// <summary>Final fare in GBP (incl. VAT). Priced at booking from the fare chart.</summary>
    public decimal? FareAmount { get; set; }

    // ─── Pricing snapshot (set at booking; see Application/Pricing) ──────────────
    // Captured at booking time so the fare is auditable and independent of later
    // fare-chart edits. Money in GBP (NUMERIC(10,2)); distance in miles.

    /// <summary>Chosen ride tier id: "economy" | "comfort" | "xl" | "premium".</summary>
    public string? Tier { get; set; }

    /// <summary>Route distance used for pricing, in miles.</summary>
    public double? DistanceMiles { get; set; }

    /// <summary>Route duration used for pricing, in minutes.</summary>
    public double? DurationMinutes { get; set; }

    /// <summary>Combined surge multiplier applied (1.0 = no surge).</summary>
    public decimal? SurgeMultiplier { get; set; }

    /// <summary>Platform (MAP CARS) fee in GBP, deducted from the fare.</summary>
    public decimal? PlatformFeeAmount { get; set; }

    /// <summary>Driver take-home in GBP (FareAmount − PlatformFeeAmount + tips).</summary>
    public decimal? DriverEarnings { get; set; }

    /// <summary>Version of the fare chart the fare was priced against.</summary>
    public int? FareChartVersion { get; set; }
}
