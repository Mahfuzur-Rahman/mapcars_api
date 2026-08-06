using Mapcars.Application.Drivers.Interfaces;
using Mapcars.Domain.Entities;
using Mapcars.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Mapcars.Infrastructure.Persistence.Repositories;

public class DriverRepository : GenericRepository<Driver>, IDriverRepository
{
    public DriverRepository(AppDbContext context) : base(context) { }

    public Task<bool> EmailExistsAsync(string email, CancellationToken ct = default)
        => Set.AnyAsync(d => d.Email == email, ct);

    public async Task<IReadOnlyList<Driver>> ListByStatusAsync(DriverStatus? status, CancellationToken ct = default)
        => await (status is null ? Set.AsNoTracking() : Set.AsNoTracking().Where(d => d.Status == status))
            .OrderByDescending(d => d.CreatedAtUtc)
            .ToListAsync(ct);

    public Task<Driver?> FindByEmailAsync(string email, CancellationToken ct = default)
        => Set.FirstOrDefaultAsync(d => d.Email == email.ToLowerInvariant().Trim(), ct);

    public Task<Driver?> FindByPhoneAsync(string phone, CancellationToken ct = default)
        => Set.FirstOrDefaultAsync(d => d.PhoneNumber == phone, ct);

    public Task<Driver?> FindByGoogleSubAsync(string googleSub, CancellationToken ct = default)
        => Set.FirstOrDefaultAsync(d => d.GoogleSub == googleSub, ct);

    public Task<bool> PhoneExistsAsync(string phone, CancellationToken ct = default)
        => Set.AnyAsync(d => d.PhoneNumber == phone, ct);

    public Task<Driver?> FindByNationalIdNumberAsync(string nationalIdNumber, CancellationToken ct = default)
        => Set.FirstOrDefaultAsync(d => d.NationalIdNumber == nationalIdNumber, ct);

    public Task<Driver?> FindByPassportNumberAsync(string passportNumber, CancellationToken ct = default)
        => Set.FirstOrDefaultAsync(d => d.PassportNumber == passportNumber, ct);
}
