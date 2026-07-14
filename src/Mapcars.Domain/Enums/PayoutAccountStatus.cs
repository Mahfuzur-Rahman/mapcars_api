namespace Mapcars.Domain.Enums;

/// <summary>Mirrors the driver's Stripe Connect Express account onboarding state.</summary>
public enum PayoutAccountStatus
{
    NotStarted = 0,
    OnboardingIncomplete = 1,
    Complete = 2,
    Restricted = 3,
}
