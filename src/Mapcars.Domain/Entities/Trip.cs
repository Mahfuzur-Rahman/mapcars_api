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

    /// <summary>Final fare in GBP (incl. VAT). Null until the trip completes.</summary>
    public decimal? FareAmount { get; set; }
}
