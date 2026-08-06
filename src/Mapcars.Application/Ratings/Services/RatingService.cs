using Mapcars.Application.Common.Exceptions;
using Mapcars.Application.Common.Interfaces;
using Mapcars.Application.Drivers.Interfaces;
using Mapcars.Application.Ratings.Dtos;
using Mapcars.Application.Ratings.Interfaces;
using Mapcars.Application.Ratings.Mapping;
using Mapcars.Application.Riders.Interfaces;
using Mapcars.Application.Trips.Interfaces;
using Mapcars.Domain.Entities;
using Mapcars.Domain.Enums;
using Mapcars.Domain.Exceptions;

namespace Mapcars.Application.Ratings.Services;

/// <summary>
/// Business logic for rider&lt;-&gt;driver ratings. A rating can only be left for a
/// completed trip, by one of its two participants, at most once per direction.
/// Submitting a rating recomputes the rated party's aggregate average/count.
/// </summary>
public class RatingService : IRatingService
{
    private readonly IRatingRepository _ratings;
    private readonly ITripRepository _trips;
    private readonly IRiderRepository _riders;
    private readonly IDriverRepository _drivers;
    private readonly IUnitOfWork _uow;

    public RatingService(
        IRatingRepository ratings, ITripRepository trips, IRiderRepository riders, IDriverRepository drivers, IUnitOfWork uow)
    {
        _ratings = ratings;
        _trips = trips;
        _riders = riders;
        _drivers = drivers;
        _uow = uow;
    }

    public async Task<RatingResponse> SubmitAsync(
        string callerType, Guid callerId, Guid tripId, SubmitRatingRequest request, CancellationToken ct = default)
    {
        var trip = await GetParticipantTripAsync(callerType, callerId, tripId, ct);

        if (trip.Status != TripStatus.Completed)
            throw new DomainException("You can only rate a completed trip.");

        var existing = await _ratings.GetByTripAndRaterTypeAsync(tripId, callerType, ct);
        if (existing is not null)
            throw new DomainException("You've already rated this trip.");

        var rating = new Rating
        {
            TripId = tripId,
            RaterType = callerType,
            Score = request.Score,
            Comment = request.Comment,
        };
        await _ratings.AddAsync(rating, ct);

        // The rater is rating the OTHER party on this trip.
        if (callerType == "rider")
        {
            if (trip.DriverId is { } driverId)
            {
                var driver = await _drivers.GetByIdAsync(driverId, ct);
                if (driver is not null) ApplyRating(driver, request.Score);
            }
        }
        else
        {
            var rider = await _riders.GetByIdAsync(trip.RiderId, ct);
            if (rider is not null) ApplyRating(rider, request.Score);
        }

        await _uow.SaveChangesAsync(ct);
        return rating.ToResponse();
    }

    public async Task<IReadOnlyList<RatingResponse>> ListForTripAsync(
        string callerType, Guid callerId, Guid tripId, CancellationToken ct = default)
    {
        await GetParticipantTripAsync(callerType, callerId, tripId, ct);
        var ratings = await _ratings.ListForTripAsync(tripId, ct);
        return ratings.Select(r => r.ToResponse()).ToList();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<Trip> GetParticipantTripAsync(string callerType, Guid callerId, Guid tripId, CancellationToken ct)
    {
        var trip = await _trips.GetByIdAsync(tripId, ct) ?? throw new NotFoundException("Trip", tripId);

        var isParticipant = (callerType == "rider" && trip.RiderId == callerId)
            || (callerType == "driver" && trip.DriverId == callerId);
        if (!isParticipant)
            throw new NotFoundException("Trip", tripId);

        return trip;
    }

    private static void ApplyRating(Rider rider, int score)
    {
        rider.AverageRating = NewAverage(rider.AverageRating, rider.RatingCount, score);
        rider.RatingCount++;
    }

    private static void ApplyRating(Driver driver, int score)
    {
        driver.AverageRating = NewAverage(driver.AverageRating, driver.RatingCount, score);
        driver.RatingCount++;
    }

    private static decimal NewAverage(decimal? oldAverage, int oldCount, int score)
        => oldAverage is null ? score : ((oldAverage.Value * oldCount) + score) / (oldCount + 1);
}
