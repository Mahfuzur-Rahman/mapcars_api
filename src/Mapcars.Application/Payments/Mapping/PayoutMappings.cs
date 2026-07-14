using Mapcars.Application.Payments.Dtos;
using Mapcars.Domain.Entities;

namespace Mapcars.Application.Payments.Mapping;

/// <summary>Manual entity &lt;-&gt; DTO mapping (no AutoMapper — explicit and fast).</summary>
public static class PayoutMappings
{
    public static PayoutAccountResponse ToResponse(this DriverPayoutAccount account) => new(
        account.Status.ToString(),
        account.PayoutsEnabled,
        account.ChargesEnabled);

    public static PayoutResponse ToResponse(this Payout payout) => new(
        payout.Id,
        payout.Amount,
        payout.Currency,
        payout.Status.ToString(),
        payout.CreatedAtUtc,
        payout.ArrivedAtUtc);
}
