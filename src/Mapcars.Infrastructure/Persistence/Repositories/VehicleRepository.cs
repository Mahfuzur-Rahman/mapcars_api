using Mapcars.Application.Vehicles.Interfaces;
using Mapcars.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Mapcars.Infrastructure.Persistence.Repositories;

public class VehicleRepository : GenericRepository<Vehicle>, IVehicleRepository
{
    public VehicleRepository(AppDbContext context) : base(context) { }

    public async Task<Vehicle?> GetByDriverAsync(Guid driverId, CancellationToken ct = default)
        => await Set.FirstOrDefaultAsync(v => v.DriverId == driverId, ct);

    public async Task<Vehicle?> GetByRegistrationAsync(string registrationNumber, CancellationToken ct = default)
        => await Set.AsNoTracking().FirstOrDefaultAsync(v => v.RegistrationNumber == registrationNumber, ct);
}
