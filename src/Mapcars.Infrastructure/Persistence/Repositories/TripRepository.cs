using Mapcars.Application.Trips.Interfaces;
using Mapcars.Domain.Entities;
using Mapcars.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Mapcars.Infrastructure.Persistence.Repositories;

public class TripRepository : GenericRepository<Trip>, ITripRepository
{
    public TripRepository(AppDbContext context) : base(context) { }

    public async Task<IReadOnlyList<Trip>> ListForRiderAsync(Guid riderId, CancellationToken ct = default)
        => await Set.AsNoTracking()
            .Where(t => t.RiderId == riderId)
            .OrderByDescending(t => t.CreatedAtUtc)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Trip>> ListForDriverAsync(Guid driverId, CancellationToken ct = default)
        => await Set.AsNoTracking()
            .Where(t => t.DriverId == driverId)
            .OrderByDescending(t => t.CreatedAtUtc)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Trip>> ListAvailableAsync(CancellationToken ct = default)
        => await Set.AsNoTracking()
            .Where(t => t.Status == TripStatus.Requested && t.DriverId == null)
            .OrderBy(t => t.CreatedAtUtc)
            .ToListAsync(ct);

    public Task<bool> HasActiveTripAsync(Guid driverId, CancellationToken ct = default)
        => Set.AsNoTracking().AnyAsync(
            t => t.DriverId == driverId &&
                 (t.Status == TripStatus.DriverAssigned ||
                  t.Status == TripStatus.DriverArrived ||
                  t.Status == TripStatus.InProgress),
            ct);

    public async Task<Trip?> GetActiveForRiderAsync(Guid riderId, CancellationToken ct = default)
        => await Set.AsNoTracking()
            .Where(t => t.RiderId == riderId &&
                        (t.Status == TripStatus.Requested ||
                         t.Status == TripStatus.DriverAssigned ||
                         t.Status == TripStatus.DriverArrived ||
                         t.Status == TripStatus.InProgress))
            .OrderByDescending(t => t.CreatedAtUtc)
            .FirstOrDefaultAsync(ct);

    public async Task<Trip?> GetActiveForDriverAsync(Guid driverId, CancellationToken ct = default)
        => await Set.AsNoTracking()
            .Where(t => t.DriverId == driverId &&
                        (t.Status == TripStatus.DriverAssigned ||
                         t.Status == TripStatus.DriverArrived ||
                         t.Status == TripStatus.InProgress))
            .OrderByDescending(t => t.CreatedAtUtc)
            .FirstOrDefaultAsync(ct);

    public async Task<bool> TryAssignAsync(Guid tripId, Guid driverId, CancellationToken ct = default)
    {
        // Single atomic UPDATE — only one caller can flip Requested→DriverAssigned.
        var rows = await Set
            .Where(t => t.Id == tripId && t.Status == TripStatus.Requested && t.DriverId == null)
            .ExecuteUpdateAsync(s => s
                .SetProperty(t => t.Status, TripStatus.DriverAssigned)
                .SetProperty(t => t.DriverId, driverId), ct);
        return rows == 1;
    }
}
