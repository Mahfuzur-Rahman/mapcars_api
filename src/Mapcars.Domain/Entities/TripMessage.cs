using Mapcars.Domain.Common;

namespace Mapcars.Domain.Entities;

/// <summary>
/// A single chat message sent during a trip, by either the rider or the driver.
/// Messages are persisted and never deleted — they're short-lived trip data.
/// </summary>
public class TripMessage : BaseEntity
{
    public Guid TripId { get; set; }
    public Trip? Trip { get; set; }

    /// <summary>Who sent this message: "rider" or "driver".</summary>
    public required string SenderType { get; set; }

    public Guid SenderId { get; set; }

    public required string Content { get; set; }

    public DateTime SentAtUtc { get; set; }
}
