namespace Mapcars.Application.Settings.Models;

/// <summary>
/// Which payment methods the platform accepts. Stored as one JSON document under
/// <see cref="SettingKeys.Payments"/>.
///
/// <para>
/// <b>Defaults matter here.</b> A fresh or unreadable settings row must fall back
/// to cash-only, never to card-enabled: the safe failure is "we take cash", not
/// "we started charging cards because a JSON blob would not parse".
/// </para>
/// </summary>
public class PaymentSettings
{
    public bool CashEnabled { get; set; } = true;

    /// <summary>
    /// Off until the Stripe work lands and has been tested. This is the kill
    /// switch: turning card payments off platform-wide is one PUT, no deploy.
    /// </summary>
    public bool CardEnabled { get; set; }

    /// <summary>
    /// Which method the apps preselect: "Cash" or "Card". Ignored by clients when
    /// it names a method that is disabled — the service corrects it on write, so
    /// clients never have to reason about an impossible combination.
    /// </summary>
    public string DefaultMethod { get; set; } = PaymentMethodNames.Cash;
}

/// <summary>
/// The wire spelling of a payment method. Matches <c>Domain.Enums.PaymentMethod</c>
/// names, which is what <c>Trip.PaymentMethod</c> persists.
/// </summary>
public static class PaymentMethodNames
{
    public const string Cash = "Cash";
    public const string Card = "Card";
}
