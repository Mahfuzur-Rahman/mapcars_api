using Mapcars.Application.Common.Interfaces;
using Mapcars.Domain.Entities;

namespace Mapcars.Application.Vehicles.Interfaces;

public interface IVehicleRepository : IGenericRepository<Vehicle>
{
    Task<Vehicle?> GetByDriverAsync(Guid driverId, CancellationToken ct = default);
    Task<Vehicle?> GetByRegistrationAsync(string registrationNumber, CancellationToken ct = default);
}
