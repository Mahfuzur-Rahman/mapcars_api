using Mapcars.Domain.Entities;
using Mapcars.Domain.Enums;

namespace Mapcars.Application.Trips.Mapping;

/// <summary>
/// What a trip's payment looks like from the driver's side of the car.
///
/// <para>
/// The platform absorbs a failed card charge — the driver is ALWAYS paid. So the
/// customer's settlement state (<see cref="PaymentStatus.Processing"/>, a decline,
/// an SCA challenge, a later refund) is none of the driver's business, and showing
/// it would put a driver in a position to confront a customer over their card at
/// the kerb. A driver only ever sees three states:
/// </para>
/// <list type="bullet">
///   <item><see cref="PaymentStatus.Pending"/> — the trip hasn't settled yet.</item>
///   <item><see cref="PaymentStatus.Collected"/> — nothing more to do: cash in hand,
///     or a card trip that has completed, however the charge went.</item>
///   <item><see cref="PaymentStatus.Voided"/> — cancelled or expired, nothing owed.</item>
/// </list>
///
/// <para>
/// This is the ONE place that rule lives. Every response a driver receives — REST
/// and the SignalR <c>tripUpdated</c> push, which goes to a driver-only group for
/// exactly this reason — is mapped through it.
/// </para>
/// </summary>
public static class DriverPaymentView
{
    public static PaymentStatus StatusFor(PaymentStatus actual) => actual switch
    {
        PaymentStatus.Pending or PaymentStatus.Collected or PaymentStatus.Voided => actual,

        // The card path after drop-off. From the driver's side the fare is settled
        // the moment the trip completes — the platform guarantees it.
        PaymentStatus.Processing
            or PaymentStatus.ActionRequired
            or PaymentStatus.Failed
            or PaymentStatus.Refunded => PaymentStatus.Collected,

        // A hold is not a settlement. Nothing writes this today.
        PaymentStatus.Authorized => PaymentStatus.Pending,

        // A member added later is private until someone decides otherwise.
        _ => PaymentStatus.Pending,
    };

    /// <summary>
    /// When the driver should consider the fare settled. The customer's real
    /// <c>PaidAtUtc</c> moves with their card (a retry can land hours later), which
    /// would leak a decline by its timing alone — so a card trip the driver sees
    /// as collected reads as paid at drop-off.
    /// </summary>
    public static DateTime? PaidAtFor(Trip trip)
    {
        if (StatusFor(trip.PaymentStatus) != PaymentStatus.Collected) return null;
        return trip.PaymentMethod == PaymentMethod.Card
            ? trip.CompletedAtUtc ?? trip.PaidAtUtc
            : trip.PaidAtUtc;
    }
}
