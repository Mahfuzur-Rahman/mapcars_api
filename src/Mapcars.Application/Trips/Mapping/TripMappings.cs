using Mapcars.Application.Trips.Dtos;
using Mapcars.Domain.Entities;

namespace Mapcars.Application.Trips.Mapping;

/// <summary>Manual entity &lt;-&gt; DTO mapping (no AutoMapper — explicit and fast).</summary>
public static class TripMappings
{
    public static TripResponse ToResponse(this Trip trip) => new(
        trip.Id,
        trip.PickupAddress,
        trip.PickupLat,
        trip.PickupLng,
        trip.DropoffAddress,
        trip.DropoffLat,
        trip.DropoffLng,
        trip.Status.ToString(),
        trip.FareAmount,
        trip.Tier,
        trip.DistanceMiles,
        trip.DurationMinutes,
        trip.SurgeMultiplier,
        trip.PlatformFeeAmount,
        trip.DriverEarnings,
        trip.FareChartVersion,
        trip.CreatedAtUtc);
}
