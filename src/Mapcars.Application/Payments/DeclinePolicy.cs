namespace Mapcars.Application.Payments;

/// <summary>What to do about a declined card.</summary>
public enum DeclineAction
{
    /// <summary>Worth trying again on the usual schedule.</summary>
    Retry,

    /// <summary>Worth trying again, but only once, after a day.</summary>
    RetryOnceAfterADay,

    /// <summary>Retrying will fail identically. Ask the customer for a different card.</summary>
    AskForAnotherCard,

    /// <summary>Not a decline at all — the customer must authenticate.</summary>
    RequiresAuthentication,
}

/// <summary>
/// The single rule for "is this decline worth retrying, and does the card stay
/// usable?". A pure function over the provider's decline code, so it is fully
/// testable with no account and no network.
///
/// <para>
/// Mirrors <c>DriverApproval</c> and <c>DriverPaymentOptions</c>: one static
/// class holding one rule.
/// </para>
/// </summary>
public static class DeclinePolicy
{
    /// <summary>
    /// How many automatic attempts to make before giving up and waiting for the
    /// customer. Beyond this the fare becomes debt and blocks new bookings.
    /// </summary>
    public const int MaxAutoRetryAttempts = 3;

    /// <summary>
    /// Fixed offsets from the failure, not exponential backoff.
    ///
    /// <para>
    /// Backoff is a server-protection pattern and protects nothing here. A card
    /// retry is about the <i>issuer's</i> state — a salary landing, a fraud hold
    /// clearing, a daily limit resetting overnight — so the useful schedule is
    /// "soon, later today, tomorrow".
    /// </para>
    /// </summary>
    public static readonly TimeSpan[] RetryOffsets =
    [
        TimeSpan.FromMinutes(15),
        TimeSpan.FromHours(2),
        TimeSpan.FromHours(24),
    ];

    public static DeclineAction ActionFor(string? declineCode) => (declineCode ?? "").ToLowerInvariant() switch
    {
        // Not a decline. Retrying unattended fails identically, forever — only an
        // on-session challenge clears it.
        "authentication_required" => DeclineAction.RequiresAuthentication,

        // Transient: the money may simply not be there yet.
        "insufficient_funds" or "generic_decline" or "try_again_later"
            or "processing_error" or "issuer_not_available" => DeclineAction.Retry,

        // Real but rate-limited by the issuer — only the overnight attempt helps.
        "card_velocity_exceeded" or "do_not_honor" => DeclineAction.RetryOnceAfterADay,

        // Permanent for this card.
        "expired_card" or "incorrect_cvc" or "incorrect_number" or "invalid_account"
            or "card_not_supported" or "currency_not_supported"
            or "lost_card" or "stolen_card" or "pickup_card" => DeclineAction.AskForAnotherCard,

        // Unknown code: retry rather than strand the fare. The attempt cap stops
        // this becoming an infinite loop, and a code we do not recognise is more
        // likely transient than permanent.
        _ => DeclineAction.Retry,
    };

    public static bool IsRetryable(string? declineCode)
        => ActionFor(declineCode) is DeclineAction.Retry or DeclineAction.RetryOnceAfterADay;

    /// <summary>
    /// Should the saved card be deactivated? Only for permanent failures — a card
    /// that merely had no money in it on Tuesday is still a perfectly good card.
    /// </summary>
    public static bool ShouldDeactivateCard(string? declineCode)
        => ActionFor(declineCode) == DeclineAction.AskForAnotherCard;

    /// <summary>
    /// When to try again, or null when there is no automatic attempt left.
    ///
    /// <para><paramref name="attemptCount"/> is how many attempts have already
    /// been made (0 = the charge at drop-off has just failed).</para>
    /// </summary>
    public static DateTime? NextRetryAtUtc(string? declineCode, int attemptCount, DateTime nowUtc)
    {
        var action = ActionFor(declineCode);
        if (action is DeclineAction.AskForAnotherCard or DeclineAction.RequiresAuthentication) return null;
        if (attemptCount >= MaxAutoRetryAttempts) return null;

        // A velocity/limit decline is pointless to retry in fifteen minutes; the
        // only attempt with a chance is the one after the issuer's day rolls over.
        if (action == DeclineAction.RetryOnceAfterADay)
            return attemptCount == 0 ? nowUtc.Add(RetryOffsets[^1]) : null;

        return nowUtc.Add(RetryOffsets[attemptCount]);
    }

    /// <summary>
    /// What to show the customer.
    ///
    /// <para>
    /// Note lost/stolen/pickup deliberately get the generic wording. Providers
    /// are explicit that a cardholder must not be told their card was reported
    /// lost or stolen — the person holding the phone may not be the person who
    /// reported it.
    /// </para>
    /// </summary>
    public static string CustomerMessageFor(string? declineCode) => (declineCode ?? "").ToLowerInvariant() switch
    {
        "insufficient_funds" => "Your card was declined for insufficient funds. We'll try again shortly.",
        "expired_card" => "That card has expired. Please add a new one to settle this trip.",
        "incorrect_cvc" or "incorrect_number" => "Those card details weren't accepted. Please add the card again.",
        "authentication_required" => "Your bank needs you to confirm this payment.",
        "card_velocity_exceeded" => "Your card issuer has temporarily blocked further payments. We'll try again tomorrow.",
        _ => "Your card was declined. Please try another card.",
    };
}
