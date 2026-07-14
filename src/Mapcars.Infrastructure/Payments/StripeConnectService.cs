using Mapcars.Application.Payments.Interfaces;
using Stripe;

namespace Mapcars.Infrastructure.Payments;

/// <summary>Stripe.net-backed implementation of the Connect seam. Requires Stripe:SecretKey configured.</summary>
public class StripeConnectService : IStripeConnectService
{
    public async Task<string> CreateExpressAccountAsync(string? email, CancellationToken ct = default)
    {
        var options = new AccountCreateOptions
        {
            Type = "express",
            Country = "GB",
            Email = email,
            Capabilities = new AccountCapabilitiesOptions
            {
                Transfers = new AccountCapabilitiesTransfersOptions { Requested = true },
            },
            BusinessType = "individual",
        };

        var service = new AccountService();
        var account = await service.CreateAsync(options, cancellationToken: ct);
        return account.Id;
    }

    public async Task<string> CreateOnboardingLinkAsync(
        string stripeAccountId, string refreshUrl, string returnUrl, CancellationToken ct = default)
    {
        var options = new AccountLinkCreateOptions
        {
            Account = stripeAccountId,
            RefreshUrl = refreshUrl,
            ReturnUrl = returnUrl,
            Type = "account_onboarding",
        };

        var service = new AccountLinkService();
        var link = await service.CreateAsync(options, cancellationToken: ct);
        return link.Url;
    }

    public async Task<StripeAccountStatus> GetAccountStatusAsync(string stripeAccountId, CancellationToken ct = default)
    {
        var service = new AccountService();
        var account = await service.GetAsync(stripeAccountId, cancellationToken: ct);
        return new StripeAccountStatus(account.DetailsSubmitted, account.PayoutsEnabled, account.ChargesEnabled);
    }
}
