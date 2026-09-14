namespace Mapcars.Domain.Enums;

/// <summary>
/// Settlement state of a trip's fare.
///
/// <para>
/// <b>Cash:</b> <see cref="Pending"/> → <see cref="Collected"/> at drop-off
/// (settled in person; no money moves through the platform).
/// </para>
///
/// <para>
/// <b>Card:</b> <see cref="Pending"/> at booking → <see cref="Processing"/> the
/// moment the trip completes → one of <see cref="Collected"/>,
/// <see cref="ActionRequired"/> or <see cref="Failed"/>. From
/// <see cref="ActionRequired"/> the customer completes SCA on-session and it
/// reaches <see cref="Collected"/>; from <see cref="Failed"/> a retry may still
/// settle it, or it stays failed and becomes customer debt.
/// </para>
///
/// <para>
/// <b>Either:</b> <see cref="Pending"/> → <see cref="Voided"/> when the trip is
/// cancelled or expires — nothing is owed and nothing will be taken.
/// </para>
///
/// <para>
/// <see cref="Collected"/> is the single terminal "the fare is settled" state,
/// shared by both rails. Clients check for it by name, so it keeps that spelling
/// even though "captured" would describe the card path more precisely — and it is
/// honest for both: cash collected in hand and card collected via Stripe are the
/// same fact.
/// </para>
///
/// <para>
/// Persisted as the member NAME (<c>HasConversion&lt;string&gt;</c>, VARCHAR(20)),
/// so adding members is safe but RENAMING one is a data migration. The longest
/// name here is <c>ActionRequired</c> at 14 characters.
/// </para>
/// </summary>
public enum PaymentStatus
{
    /// <summary>Booked; nothing settled yet.</summary>
    Pending,

    /// <summary>TERMINAL-PAID. Cash in hand, or a card charge captured.</summary>
    Collected,

    /// <summary>The card was declined and has not (yet) been recovered — customer debt.</summary>
    Failed,

    // ─── added for card payments ────────────────────────────────────────────

    /// <summary>
    /// The trip is complete and a charge is in flight, or about to be. Written
    /// before any call to the payment provider, so a trip in this state with no
    /// provider reference is exactly what the recovery sweeper looks for.
    /// </summary>
    Processing,

    /// <summary>
    /// The provider needs the customer to authenticate (SCA). Only an on-session
    /// challenge clears this — retrying off-session fails identically, forever.
    /// </summary>
    ActionRequired,

    /// <summary>Fully refunded after having been <see cref="Collected"/>.</summary>
    Refunded,

    /// <summary>
    /// The trip was cancelled or expired, so nothing is owed and nothing will be
    /// taken.
    ///
    /// <para>
    /// This is the member that fixes an existing lie: cancelled trips kept
    /// <see cref="Pending"/> forever, which reads as an unsettled fare in every
    /// "what is outstanding?" query and on the admin transactions view.
    /// </para>
    /// </summary>
    Voided,

    /// <summary>
    /// RESERVED. Authorise-at-booking is deliberately NOT built: the chosen model
    /// saves the card and charges at drop-off. Nothing writes this. It exists so
    /// the state machine documents where a manual-capture hold would sit
    /// (<see cref="Pending"/> → Authorized → <see cref="Collected"/>) if that
    /// switch is ever flipped.
    /// </summary>
    Authorized,
}
