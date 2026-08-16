using Mapcars.Domain.Common;
using Mapcars.Domain.Enums;

namespace Mapcars.Domain.Entities;

/// <summary>
/// A driver's request to have their vehicle's ride tier upgraded (e.g. from Economy to Comfort/XL/Premium).
/// Reviewed and decided by an administrator.
/// </summary>
public class VehicleTierAppeal : BaseEntity
{
    public Guid DriverId { get; set; }
    public Driver? Driver { get; set; }

    public Guid VehicleId { get; set; }
    public Vehicle? Vehicle { get; set; }

    public required string CurrentTier { get; set; }
    public required string RequestedTier { get; set; }
    public required string Reason { get; set; }

    /// <summary>Storage keys for optional car images uploaded by the driver.</summary>
    public List<string> PhotoStorageKeys { get; set; } = new();

    public TierAppealStatus Status { get; set; } = TierAppealStatus.Pending;

    public string? AdminNotes { get; set; }

    public Guid? ReviewedByAdminId { get; set; }
    public Admin? ReviewedByAdmin { get; set; }

    public DateTime? ReviewedAtUtc { get; set; }
}
