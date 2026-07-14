namespace Mapcars.Domain.Enums;

/// <summary>Mirrors Stripe's payout.status values.</summary>
public enum PayoutStatus
{
    Pending = 0,
    InTransit = 1,
    Paid = 2,
    Failed = 3,
    Canceled = 4,
}
