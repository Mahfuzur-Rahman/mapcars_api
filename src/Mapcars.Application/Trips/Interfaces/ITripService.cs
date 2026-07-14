using Mapcars.Application.Pricing.Dtos;
using Mapcars.Application.Trips.Dtos;

namespace Mapcars.Application.Trips.Interfaces;

/// <summary>Trip use-cases (business logic layer surface).</summary>
public interface ITripService
{
    Task<IReadOnlyList<TripResponse>> ListForRiderAsync(Guid riderId, CancellationToken ct = default);

    /// <summary>
    /// Books a trip for a rider. Prices the chosen tier authoritatively from the
    /// current fare chart and stores the fare breakdown on the trip.
    /// </summary>
    Task<TripResponse> CreateAsync(Guid riderId, CreateTripRequest request, CancellationToken ct = default);
}
