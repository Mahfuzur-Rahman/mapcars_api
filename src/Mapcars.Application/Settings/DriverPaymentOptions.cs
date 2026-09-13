using Mapcars.Application.Settings.Models;
using Mapcars.Domain.Entities;

namespace Mapcars.Application.Settings;

/// <summary>
/// The single rule for "which payment methods may this driver take?".
///
/// <para>
/// <b>The global setting is a ceiling.</b> A per-driver override can only ever
/// narrow it, never widen it. Once cash is switched off platform-wide, no
/// per-driver flag brings it back — otherwise "cash is off" would not actually be
/// true, and that guarantee is the entire point of the switch.
/// </para>
///
/// <para>
/// A null override means "follow the global setting", which is why the columns
/// are nullable booleans rather than defaulted ones — see migration 034.
/// </para>
///
/// <para>
/// Mirrors <c>DriverApproval</c>: one static rule, stated once, used by every
/// caller that needs it.
/// </para>
/// </summary>
public static class DriverPaymentOptions
{
    public static bool AcceptsCash(PaymentSettings settings, Driver driver)
        => settings.CashEnabled && (driver.AcceptsCashOverride ?? true);

    public static bool AcceptsCard(PaymentSettings settings, Driver driver)
        => settings.CardEnabled && (driver.AcceptsCardOverride ?? true);

    /// <summary>Can this driver be offered a trip paid by this method?</summary>
    public static bool Accepts(PaymentSettings settings, Driver driver, Domain.Enums.PaymentMethod method)
        => method == Domain.Enums.PaymentMethod.Cash
            ? AcceptsCash(settings, driver)
            : AcceptsCard(settings, driver);

    /// <summary>Why a driver may not take this trip, in words an admin can act on.</summary>
    public static string BlockedMessage(Domain.Enums.PaymentMethod method)
        => method == Domain.Enums.PaymentMethod.Cash
            ? "This trip is paid in cash, and your account is set to card payments only."
            : "This trip is paid by card, and your account is set to cash only.";
}
