namespace Mapcars.Application.Settings.Dtos;

/// <summary>
/// What a CLIENT is told. Anonymous, read by both apps at launch to decide what
/// to render on the booking sheet.
///
/// <para>
/// <b>Deliberately only the three method fields.</b> The fraud thresholds live
/// in the same settings document but are not published here — telling the world
/// "you get 5 card attempts a day and bookings block above £0 of debt" hands an
/// attacker the shape of every limit they need to stay under. The admin response
/// below carries them; this one never will.
/// </para>
/// </summary>
public record PaymentSettingsResponse(
    bool CashEnabled,
    bool CardEnabled,
    string DefaultMethod);

/// <summary>
/// The full settings document, for the admin portal only.
/// </summary>
public record AdminPaymentSettingsResponse(
    bool CashEnabled,
    bool CardEnabled,
    string DefaultMethod,
    // step-up verification
    bool ChallengeOnNewDevice,
    bool ChallengeAfterFailedCharge,
    bool ChallengeUnauthenticatedCards,
    int ChallengeAboveFarePence,
    int ReverifyAfterDormantDays,
    // limits
    int MaxSavedCardsPerCustomer,
    int MaxCardAddAttemptsPerDay,
    int BlockBookingWhenDebtExceedsPence);

/// <summary>SuperAdmin edit of the whole document.</summary>
public class UpdatePaymentSettingsRequest
{
    public bool CashEnabled { get; set; }
    public bool CardEnabled { get; set; }
    public string DefaultMethod { get; set; } = string.Empty;

    public bool ChallengeOnNewDevice { get; set; }
    public bool ChallengeAfterFailedCharge { get; set; }
    public bool ChallengeUnauthenticatedCards { get; set; }
    public int ChallengeAboveFarePence { get; set; }
    public int ReverifyAfterDormantDays { get; set; }

    public int MaxSavedCardsPerCustomer { get; set; }
    public int MaxCardAddAttemptsPerDay { get; set; }
    public int BlockBookingWhenDebtExceedsPence { get; set; }
}

/// <summary>A driver's per-driver override, as the admin portal sees it.</summary>
public record DriverPaymentOptionsResponse(
    Guid DriverId,
    bool? AcceptsCashOverride,
    bool? AcceptsCardOverride,
    bool EffectiveAcceptsCash,
    bool EffectiveAcceptsCard);

/// <summary>
/// Null means "follow the global setting" — the tri-state is deliberate and is
/// carried all the way to the wire, so an admin can clear an override rather than
/// only ever flipping it.
/// </summary>
public class UpdateDriverPaymentOptionsRequest
{
    public bool? AcceptsCashOverride { get; set; }
    public bool? AcceptsCardOverride { get; set; }
}
