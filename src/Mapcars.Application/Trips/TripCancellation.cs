using Mapcars.Domain.Enums;

namespace Mapcars.Application.Trips;

/// <summary>
/// Who may cancel a trip, and from which status. <b>The single source of truth</b>
/// for that rule — <c>TripService.CancelAsync</c> asks here and nowhere else.
///
/// <para>
/// Cancellation is only for a ride that has not started yet. Once the passenger
/// is in the car (<see cref="TripStatus.InProgress"/>) a cancel would void a fare
/// that is being ridden and leave no commission owed; and an
/// <see cref="TripStatus.Expired"/> request was the platform failing to find a
/// driver, which must not be rewritten as the customer walking away and counted
/// against them.
/// </para>
///
/// <para>
/// A driver has nothing to cancel before they accept: a
/// <see cref="TripStatus.Requested"/> trip has no driver, and declining it is
/// simply not accepting it.
/// </para>
/// </summary>
public static class TripCancellation
{
    public static bool CanCancel(TripStatus status, bool byDriver) => byDriver
        ? status is TripStatus.DriverAssigned or TripStatus.DriverArrived
        : status is TripStatus.Requested or TripStatus.DriverAssigned or TripStatus.DriverArrived;
}
