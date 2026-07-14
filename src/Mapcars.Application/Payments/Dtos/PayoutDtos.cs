namespace Mapcars.Application.Payments.Dtos;

public record StartOnboardingRequest(string RefreshUrl, string ReturnUrl);

public record OnboardingLinkResponse(string Url);

/// <summary>Outbound payout-account representation. Never expose entities directly.</summary>
public record PayoutAccountResponse(
    string Status,
    bool PayoutsEnabled,
    bool ChargesEnabled);

/// <summary>Outbound payout representation. Never expose entities directly.</summary>
public record PayoutResponse(
    Guid Id,
    decimal Amount,
    string Currency,
    string Status,
    DateTime CreatedAtUtc,
    DateTime? ArrivedAtUtc);
