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
    public static TripResponse ToResponse(this Trip trip, TripDriverInfo? driver = null) => new(
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
        driver);
}
