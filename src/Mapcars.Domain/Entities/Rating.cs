using Mapcars.Domain.Common;

namespace Mapcars.Domain.Entities;

/// <summary>
/// A 1-5 star rating left for a completed trip, in one direction (rider rates
/// driver, or driver rates rider). At most one per (TripId, RaterType).
/// </summary>
public class Rating : BaseEntity
{
    public Guid TripId { get; set; }
    public Trip? Trip { get; set; }

    /// <summary>Who submitted this rating: "rider" or "driver".</summary>
    public required string RaterType { get; set; }

    public int Score { get; set; }
    public string? Comment { get; set; }
}
