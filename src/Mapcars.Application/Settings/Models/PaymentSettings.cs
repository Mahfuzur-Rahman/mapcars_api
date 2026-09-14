namespace Mapcars.Application.Settings.Models;

/// <summary>
/// Every knob that controls how Mapcars takes money. Stored as one JSON document
/// under <see cref="SettingKeys.Payments"/>, published by a SuperAdmin, live
/// immediately with no deploy.
///
/// <para>
/// <b>Adding a setting here needs no migration.</b> The store keeps this as
/// JSONB, and a document written before a property existed simply deserialises
/// with that property's default — which is why every default below is the safe
/// choice rather than the permissive one.
/// </para>
///
/// <para>
/// <b>Defaults must fail closed.</b> A fresh or unreadable row must land on
/// cash-only with protections on. The safe failure is "we take cash"; the unsafe
/// one is "we started charging cards because a JSON blob would not parse".
/// </para>
///
/// <para>See <c>PAYMENTS_CONFIG.md</c> for the plain-English version.</para>
/// </summary>
public class PaymentSettings
{
    // ─── Which methods customers may use ──────────────────────────────────────

    public bool CashEnabled { get; set; } = true;

    /// <summary>
    /// Off until Stripe is configured and tested. This is the kill switch:
    /// turning card payments off platform-wide is one PUT, no deploy.
    /// </summary>
    public bool CardEnabled { get; set; }

    /// <summary>
    /// Which method the apps preselect: "Cash" or "Card". Never names a disabled
    /// method — the service corrects it on write rather than rejecting the change.
    /// </summary>
    public string DefaultMethod { get; set; } = PaymentMethodNames.Cash;

    // ─── Step-up verification ─────────────────────────────────────────────────
    //
    // Re-authenticating a saved card is triggered by RISK, never by a calendar.
    //
    // A monthly "re-verify everyone" rule sounds safer and is not: ride-hailing
    // card fraud runs its course in days, so a 30-day cycle never meets the
    // attacker — it only adds a bank challenge for the loyal customer who has had
    // a card saved for months. The triggers below aim the same tool at the
    // moments where risk actually spikes.

    /// <summary>
    /// Challenge when the customer signs in from a device we have not seen.
    ///
    /// <para>
    /// The strongest of these. It is the one trigger that catches account
    /// takeover, because an attacker inside someone's Mapcars account still does
    /// not have their banking app.
    /// </para>
    /// </summary>
    public bool ChallengeOnNewDevice { get; set; } = true;

    /// <summary>
    /// Challenge before letting a customer book again after a charge failed.
    /// Cheap: they are already interrupted, so the friction costs nothing extra.
    /// </summary>
    public bool ChallengeAfterFailedCharge { get; set; } = true;

    /// <summary>
    /// Challenge cards that were never actually authenticated when saved.
    ///
    /// <para>
    /// Targeted using what the card itself records — see
    /// <c>CustomerPaymentMethod.IsLikelyLiabilityShifted</c>. A card saved with a
    /// real authentication generally leaves fraud liability with the issuer; one
    /// saved frictionless does not. This asks for the challenge only on the
    /// second kind, which is a far sharper filter than "time has passed".
    /// </para>
    /// </summary>
    public bool ChallengeUnauthenticatedCards { get; set; } = true;

    /// <summary>
    /// Fare threshold, in pence, above which <see cref="ChallengeUnauthenticatedCards"/>
    /// applies. 0 means always.
    ///
    /// <para>
    /// Default £25. The point is to spend the friction where a loss actually
    /// hurts — an airport run, not an £8 hop — because challenging every trip
    /// costs more in abandoned bookings than it saves in fraud.
    /// </para>
    /// </summary>
    public int ChallengeAboveFarePence { get; set; } = 2500;

    /// <summary>
    /// Re-verify when an account wakes after this many days idle. 0 disables it.
    ///
    /// <para>
    /// Off by default. This is the closest thing here to the calendar rule, and
    /// it is deliberately narrow: a long-dormant account reactivating is a real
    /// resale/compromise signal, whereas "everyone, monthly" is not.
    /// </para>
    /// </summary>
    public int ReverifyAfterDormantDays { get; set; }

    // ─── Limits ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Saved cards per customer. A real customer needs two or three; a card
    /// tester needs hundreds.
    /// </summary>
    public int MaxSavedCardsPerCustomer { get; set; } = 5;

    /// <summary>
    /// Card-add attempts per customer per day, counting failures.
    ///
    /// <para>
    /// This is the one with teeth against card testing. The IP rate limit in
    /// <c>Program.cs</c> stops the casual case, but rotating IPs is trivial — a
    /// per-ACCOUNT cap follows the person instead of the connection.
    /// </para>
    /// </summary>
    public int MaxCardAddAttemptsPerDay { get; set; } = 5;

    /// <summary>
    /// Unpaid balance, in pence, above which new bookings are refused.
    /// 0 means block on any settled debt at all.
    ///
    /// <para>
    /// This is the cap on blast radius: it is what stops one compromised card
    /// funding ten rides before anybody notices.
    /// </para>
    /// </summary>
    public int BlockBookingWhenDebtExceedsPence { get; set; }
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
