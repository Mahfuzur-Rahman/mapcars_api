using Mapcars.Application.Common.Interfaces;
using Mapcars.Application.Pricing.Dtos;
using Mapcars.Application.Pricing.Interfaces;
using Mapcars.Application.Trips.Dtos;
using Mapcars.Application.Trips.Interfaces;
using Mapcars.Application.Trips.Mapping;
using Mapcars.Domain.Entities;

namespace Mapcars.Application.Trips.Services;

/// <summary>
/// Business logic for trips. Listing is read-only; booking (<see cref="CreateAsync"/>)
/// prices the chosen tier authoritatively from the fare chart and snapshots the
/// breakdown onto the trip so it's independent of later chart edits.
/// </summary>
public class TripService : ITripService
{
    private readonly ITripRepository _trips;
    private readonly IPricingService _pricing;
    private readonly IUnitOfWork _uow;

    public TripService(ITripRepository trips, IPricingService pricing, IUnitOfWork uow)
    {
        _trips = trips;
        _pricing = pricing;
        _uow = uow;
    }

    public async Task<IReadOnlyList<TripResponse>> ListForRiderAsync(Guid riderId, CancellationToken ct = default)
    {
        var trips = await _trips.ListForRiderAsync(riderId, ct);
        return trips.Select(t => t.ToResponse()).ToList();
    }

    public async Task<TripResponse> CreateAsync(Guid riderId, CreateTripRequest req, CancellationToken ct = default)
    {
        // Authoritative price for the chosen tier (route metrics are clamped inside).
        var fare = await _pricing.PriceTierAsync(
            req.RideOptionId,
            req.PickupLat, req.PickupLng, req.DropoffLat, req.DropoffLng,
            req.DistanceMiles, req.DurationMinutes, ct);

        var chart = await _pricing.GetChartAsync(ct);

        var trip = new Trip
        {
            RiderId = riderId,
            PickupAddress = req.PickupAddress,
            PickupLat = req.PickupLat,
            PickupLng = req.PickupLng,
            DropoffAddress = req.DropoffAddress,
            DropoffLat = req.DropoffLat,
            DropoffLng = req.DropoffLng,
            Status = Domain.Enums.TripStatus.Requested,

            Tier = fare.TierId,
            DistanceMiles = req.DistanceMiles,
            DurationMinutes = req.DurationMinutes,
            SurgeMultiplier = fare.SurgeMultiplier,
            FareAmount = Gbp(fare.FarePence),
            PlatformFeeAmount = Gbp(fare.PlatformFeePence),
            DriverEarnings = Gbp(fare.DriverEarningsPence),
            FareChartVersion = chart.Version,
        };

        await _trips.AddAsync(trip, ct);
        await _uow.SaveChangesAsync(ct);

        return trip.ToResponse();
    }

    private static decimal Gbp(int pence) => pence / 100m;
}
