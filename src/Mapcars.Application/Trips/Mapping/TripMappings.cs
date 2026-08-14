using Mapcars.Application.Trips.Dtos;
using Mapcars.Domain.Entities;

namespace Mapcars.Application.Trips.Mapping;

/// <summary>Manual entity &lt;-&gt; DTO mapping (no AutoMapper — explicit and fast).</summary>
public static class TripMappings
{
    /// <param name="driver">
    /// The assigned driver's public details, when the caller already looked
    /// them up (see <c>TripService.BuildDriverInfoAsync</c>) — omitted for
    /// contexts that don't need them (e.g. the driver's own board).
    /// </param>
    /// <param name="rider">
    /// The rider's public details — only for the trip's own two parties.
    /// </param>
    /// <param name="includePin">
    /// Whether to expose the meet-up PIN. Off by default: the open dispatch
    /// board is sent to every nearby driver, and a PIN that leaks there would
    /// let a driver who never took the trip recite it at the kerb, which is
    /// precisely what the PIN exists to prevent. Only the trip's rider and its
    /// assigned driver get it.
    /// </param>
    public static TripResponse ToResponse(
        this Trip trip,
        TripDriverInfo? driver = null,
        TripRiderInfo? rider = null,
        bool includePin = false) => new(
        trip.Id,
        trip.PickupAddress,
        trip.PickupLat,
        trip.PickupLng,
        trip.DropoffAddress,
        trip.DropoffLat,
        trip.DropoffLng,
        trip.Status.ToString(),
        trip.FareAmount,
        trip.TipAmount,
        trip.Tier,
        trip.DistanceMiles,
        trip.DurationMinutes,
        trip.SurgeMultiplier,
        trip.PlatformFeeAmount,
        trip.DriverEarnings,
        trip.FareChartVersion,
        trip.PaymentMethod.ToString(),
        trip.PaymentStatus.ToString(),
        trip.PaidAtUtc,
        trip.CreatedAtUtc,
        trip.CompletedAtUtc,
        trip.CancelledAtUtc,
        trip.CancelledReason,
        trip.IsNoShow,
        driver,
        rider,
        includePin ? trip.Pin : null);
}
