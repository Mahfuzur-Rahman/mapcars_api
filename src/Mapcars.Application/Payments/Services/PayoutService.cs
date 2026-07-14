using Mapcars.Application.Common.Interfaces;
using Mapcars.Application.Payments.Dtos;
using Mapcars.Application.Payments.Interfaces;
using Mapcars.Application.Payments.Mapping;
using Mapcars.Domain.Entities;
using Mapcars.Domain.Enums;

namespace Mapcars.Application.Payments.Services;

/// <summary>
/// Business logic for driver payouts. Orchestrates the Stripe Connect seam
/// (IStripeConnectService) with the locally cached account/payout rows, which
/// are the source of truth the API returns — kept in sync by the Stripe
/// webhook (see StripeWebhookController) rather than polling Stripe on read.
/// </summary>
public class PayoutService : IPayoutService
{
    private readonly IDriverPayoutAccountRepository _accounts;
    private readonly IPayoutRepository _payouts;
    private readonly IStripeConnectService _stripe;
    private readonly IUnitOfWork _uow;

    public PayoutService(
        IDriverPayoutAccountRepository accounts,
        IPayoutRepository payouts,
        IStripeConnectService stripe,
        IUnitOfWork uow)
    {
        _accounts = accounts;
        _payouts = payouts;
        _stripe = stripe;
        _uow = uow;
    }

    public async Task<OnboardingLinkResponse> StartOnboardingAsync(
        Guid driverId, string? driverEmail, string refreshUrl, string returnUrl, CancellationToken ct = default)
    {
        var account = await _accounts.FindByDriverIdAsync(driverId, ct);
        if (account is null)
        {
            var stripeAccountId = await _stripe.CreateExpressAccountAsync(driverEmail, ct);
            account = new DriverPayoutAccount
            {
                DriverId = driverId,
                StripeAccountId = stripeAccountId,
                Status = PayoutAccountStatus.OnboardingIncomplete,
            };
            await _accounts.AddAsync(account, ct);
            await _uow.SaveChangesAsync(ct);
        }

        var url = await _stripe.CreateOnboardingLinkAsync(account.StripeAccountId, refreshUrl, returnUrl, ct);
        return new OnboardingLinkResponse(url);
    }

    public async Task<PayoutAccountResponse> GetAccountStatusAsync(Guid driverId, CancellationToken ct = default)
    {
        var account = await _accounts.FindByDriverIdAsync(driverId, ct);
        if (account is null)
            return new PayoutAccountResponse(PayoutAccountStatus.NotStarted.ToString(), false, false);

        // Refresh from Stripe on read — cheap, low-frequency (dashboard load) — so
        // status is accurate immediately after onboarding, without waiting for the
        // webhook to arrive.
        var live = await _stripe.GetAccountStatusAsync(account.StripeAccountId, ct);
        ApplyStatus(account, live.DetailsSubmitted, live.PayoutsEnabled, live.ChargesEnabled);
        await _uow.SaveChangesAsync(ct);

        return account.ToResponse();
    }

    public async Task<IReadOnlyList<PayoutResponse>> ListPayoutsAsync(Guid driverId, CancellationToken ct = default)
    {
        var payouts = await _payouts.ListForDriverAsync(driverId, ct);
        return payouts.Select(p => p.ToResponse()).ToList();
    }

    public async Task SyncAccountStatusAsync(
        string stripeAccountId, bool detailsSubmitted, bool payoutsEnabled, bool chargesEnabled, CancellationToken ct = default)
    {
        var account = await _accounts.FindByStripeAccountIdAsync(stripeAccountId, ct);
        if (account is null) return; // Unknown account — nothing local to sync.

        ApplyStatus(account, detailsSubmitted, payoutsEnabled, chargesEnabled);
        await _uow.SaveChangesAsync(ct);
    }

    public async Task UpsertPayoutAsync(
        string stripeAccountId,
        string stripePayoutId,
        decimal amount,
        string currency,
        string status,
        DateTime? arrivedAtUtc,
        CancellationToken ct = default)
    {
        var account = await _accounts.FindByStripeAccountIdAsync(stripeAccountId, ct);
        if (account is null) return; // Unknown account — nothing local to sync.

        var payoutStatus = Enum.TryParse<PayoutStatus>(status, ignoreCase: true, out var parsed)
            ? parsed
            : PayoutStatus.Pending;

        var payout = await _payouts.FindByStripePayoutIdAsync(stripePayoutId, ct);
        if (payout is null)
        {
            payout = new Payout
            {
                DriverId = account.DriverId,
                StripePayoutId = stripePayoutId,
                Amount = amount,
                Currency = currency,
                Status = payoutStatus,
                ArrivedAtUtc = arrivedAtUtc,
            };
            await _payouts.AddAsync(payout, ct);
        }
        else
        {
            payout.Status = payoutStatus;
            payout.ArrivedAtUtc = arrivedAtUtc;
        }

        await _uow.SaveChangesAsync(ct);
    }

    private static void ApplyStatus(DriverPayoutAccount account, bool detailsSubmitted, bool payoutsEnabled, bool chargesEnabled)
    {
        account.PayoutsEnabled = payoutsEnabled;
        account.ChargesEnabled = chargesEnabled;
        account.Status = (payoutsEnabled, chargesEnabled) switch
        {
            (true, true) => PayoutAccountStatus.Complete,
            _ when account.Status == PayoutAccountStatus.Complete => PayoutAccountStatus.Restricted,
            _ when detailsSubmitted => PayoutAccountStatus.OnboardingIncomplete,
            _ => PayoutAccountStatus.OnboardingIncomplete,
        };
    }
}
