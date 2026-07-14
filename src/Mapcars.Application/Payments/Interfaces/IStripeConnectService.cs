namespace Mapcars.Application.Payments.Interfaces;

/// <summary>Thin seam over the Stripe Connect SDK — keeps Stripe.net out of the Application layer.</summary>
public interface IStripeConnectService
{
    /// <summary>Creates a new Express connected account for a driver and returns its Stripe account id.</summary>
    Task<string> CreateExpressAccountAsync(string? email, CancellationToken ct = default);

    /// <summary>Creates a one-time onboarding link URL for the given connected account.</summary>
    Task<string> CreateOnboardingLinkAsync(
        string stripeAccountId, string refreshUrl, string returnUrl, CancellationToken ct = default);

    /// <summary>Fetches the connected account's current onboarding/capability flags from Stripe.</summary>
    Task<StripeAccountStatus> GetAccountStatusAsync(string stripeAccountId, CancellationToken ct = default);
}

public record StripeAccountStatus(bool DetailsSubmitted, bool PayoutsEnabled, bool ChargesEnabled);
