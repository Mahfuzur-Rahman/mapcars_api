using Mapcars.Domain.Common;
using Mapcars.Domain.Enums;

namespace Mapcars.Domain.Entities;

/// <summary>A driver's Stripe Connect Express account, one per driver.</summary>
public class DriverPayoutAccount : BaseEntity
{
    public Guid DriverId { get; set; }
    public Driver? Driver { get; set; }

    public required string StripeAccountId { get; set; }
    public PayoutAccountStatus Status { get; set; } = PayoutAccountStatus.NotStarted;
    public bool PayoutsEnabled { get; set; }
    public bool ChargesEnabled { get; set; }
}
