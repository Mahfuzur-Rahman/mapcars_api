using Mapcars.Domain.Common;
using Mapcars.Domain.Enums;

namespace Mapcars.Domain.Entities;

/// <summary>A payout Stripe sent (or is sending) to a driver's connected account. Synced via webhook.</summary>
public class Payout : BaseEntity
{
    public Guid DriverId { get; set; }
    public Driver? Driver { get; set; }

    public required string StripePayoutId { get; set; }
    public decimal Amount { get; set; }
    public required string Currency { get; set; }
    public PayoutStatus Status { get; set; } = PayoutStatus.Pending;
    public DateTime? ArrivedAtUtc { get; set; }
}
