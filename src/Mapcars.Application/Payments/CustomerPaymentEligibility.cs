using Mapcars.Application.Common;
using Mapcars.Application.Settings.Models;
using Mapcars.Domain.Enums;

namespace Mapcars.Application.Payments;

/// <summary>
/// The single rule for "may this customer book, paying this way?". Mirrors
/// <c>DriverApproval</c> and <c>DriverPaymentOptions</c>: one pure rule, stated
/// once, used by every caller that needs it.
///
/// <para>
/// Until this existed, booking never consulted the payment settings at all. With
/// Card switched off platform-wide, a customer (or a stale app build) could still
/// book a Card trip — one no code would ever charge, so the fare was simply lost.
/// The admin switches are only real if booking obeys them.
/// </para>
/// </summary>
public static class CustomerPaymentEligibility
{
    /// <param name="hasUsableCard">The customer has an active, unexpired saved card.</param>
    /// <param name="unpaidDebtPence">Sum of this customer's card fares that failed and were never recovered.</param>
    public static Result Check(
        PaymentSettings settings, PaymentMethod method, bool hasUsableCard, int unpaidDebtPence)
    {
        // Debt first: it applies whatever the method. Paying cash does not settle
        // an unpaid card fare, and "book in cash instead" is exactly how one
        // compromised card funds ten rides.
        if (unpaidDebtPence > 0 && unpaidDebtPence > settings.BlockBookingWhenDebtExceedsPence)
            return Result.Blocked(
                $"You have an unpaid balance of {Money.Format(unpaidDebtPence)} from an earlier trip. " +
                "Please settle it before booking again.");

        return method switch
        {
            PaymentMethod.Cash when !settings.CashEnabled =>
                Result.Blocked("Cash payments aren't available right now. Please choose card."),

            PaymentMethod.Card when !settings.CardEnabled =>
                Result.Blocked("Card payments aren't available yet. Please choose cash."),

            PaymentMethod.Card when !hasUsableCard =>
                Result.Blocked("Add a card to pay by card."),

            _ => Result.Ok,
        };
    }

    public readonly record struct Result(bool Allowed, string? Reason)
    {
        public static Result Ok => new(true, null);
        public static Result Blocked(string reason) => new(false, reason);
    }
}
