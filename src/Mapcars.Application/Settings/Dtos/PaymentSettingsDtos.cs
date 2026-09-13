namespace Mapcars.Application.Settings.Dtos;

/// <summary>
/// The payment methods a client may offer. Public: both apps read this at launch
/// to decide what to render, exactly as they already do with the fare chart.
///
/// <para>
/// Note there is no Stripe publishable key here yet. It belongs with the card
/// work, and adding it now would mean the Application layer reaching into
/// Infrastructure's StripeOptions for a field nothing reads.
/// </para>
/// </summary>
public record PaymentSettingsResponse(
    bool CashEnabled,
    bool CardEnabled,
    string DefaultMethod);

/// <summary>SuperAdmin edit of the global toggles.</summary>
public class UpdatePaymentSettingsRequest
{
    public bool CashEnabled { get; set; }
    public bool CardEnabled { get; set; }
    public string DefaultMethod { get; set; } = string.Empty;
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
