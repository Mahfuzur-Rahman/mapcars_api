using Mapcars.Application.Common.Interfaces;
using Mapcars.Domain.Entities;
using Mapcars.Domain.Enums;

namespace Mapcars.Application.Vehicles.Interfaces;

public interface IVehicleTierAppealRepository : IGenericRepository<VehicleTierAppeal>
{
    Task<VehicleTierAppeal?> GetByIdWithDetailsAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<VehicleTierAppeal>> ListForDriverAsync(Guid driverId, CancellationToken ct = default);
    Task<VehicleTierAppeal?> GetActivePendingForDriverAsync(Guid driverId, CancellationToken ct = default);
    Task<IReadOnlyList<VehicleTierAppeal>> ListAllAsync(TierAppealStatus? status = null, CancellationToken ct = default);
}
