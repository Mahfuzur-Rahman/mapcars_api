using Mapcars.Application.Vehicles.Dtos;

namespace Mapcars.Application.Vehicles.Interfaces;

/// <summary>Vehicle use-cases (business logic layer surface).</summary>
public interface IVehicleService
{
    Task<VehicleResponse?> GetForDriverAsync(Guid driverId, CancellationToken ct = default);
    Task<VehicleResponse> UpsertForDriverAsync(Guid driverId, UpsertVehicleRequest request, CancellationToken ct = default);
}
