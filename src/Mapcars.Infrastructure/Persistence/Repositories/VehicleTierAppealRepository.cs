using Mapcars.Application.Vehicles.Interfaces;
using Mapcars.Domain.Entities;
using Mapcars.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Mapcars.Infrastructure.Persistence.Repositories;

public class VehicleTierAppealRepository : GenericRepository<VehicleTierAppeal>, IVehicleTierAppealRepository
{
    public VehicleTierAppealRepository(AppDbContext context) : base(context) { }

    public async Task<VehicleTierAppeal?> GetByIdWithDetailsAsync(Guid id, CancellationToken ct = default)
        => await Set
            .Include(a => a.Driver)
            .Include(a => a.Vehicle)
            .Include(a => a.ReviewedByAdmin)
            .FirstOrDefaultAsync(a => a.Id == id, ct);

    public async Task<IReadOnlyList<VehicleTierAppeal>> ListForDriverAsync(Guid driverId, CancellationToken ct = default)
        => await Set
            .Include(a => a.Vehicle)
            .Where(a => a.DriverId == driverId)
            .OrderByDescending(a => a.CreatedAtUtc)
            .ToListAsync(ct);

    public async Task<VehicleTierAppeal?> GetActivePendingForDriverAsync(Guid driverId, CancellationToken ct = default)
        => await Set
            .FirstOrDefaultAsync(a => a.DriverId == driverId && a.Status == TierAppealStatus.Pending, ct);

    public async Task<IReadOnlyList<VehicleTierAppeal>> ListAllAsync(TierAppealStatus? status = null, CancellationToken ct = default)
    {
        var query = Set
            .Include(a => a.Driver)
            .Include(a => a.Vehicle)
            .Include(a => a.ReviewedByAdmin)
            .AsQueryable();

        if (status.HasValue)
            query = query.Where(a => a.Status == status.Value);

        return await query.OrderByDescending(a => a.CreatedAtUtc).ToListAsync(ct);
    }
}
