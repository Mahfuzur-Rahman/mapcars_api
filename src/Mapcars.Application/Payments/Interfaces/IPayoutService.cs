using Mapcars.Application.Payments.Dtos;

namespace Mapcars.Application.Payments.Interfaces;

/// <summary>Driver payout use-cases (business logic layer surface).</summary>
public interface IPayoutService
{
    Task<OnboardingLinkResponse> StartOnboardingAsync(
        Guid driverId, string? driverEmail, string refreshUrl, string returnUrl, CancellationToken ct = default);

    Task<PayoutAccountResponse> GetAccountStatusAsync(Guid driverId, CancellationToken ct = default);

    Task<IReadOnlyList<PayoutResponse>> ListPayoutsAsync(Guid driverId, CancellationToken ct = default);

    /// <summary>Applies a Stripe `account.updated` webhook event to the locally cached account row.</summary>
    Task SyncAccountStatusAsync(
        string stripeAccountId, bool detailsSubmitted, bool payoutsEnabled, bool chargesEnabled, CancellationToken ct = default);

    /// <summary>Applies a Stripe `payout.*` webhook event, creating or updating the local payout row.</summary>
    Task UpsertPayoutAsync(
        string stripeAccountId,
        string stripePayoutId,
        decimal amount,
        string currency,
        string status,
        DateTime? arrivedAtUtc,
        CancellationToken ct = default);
}
