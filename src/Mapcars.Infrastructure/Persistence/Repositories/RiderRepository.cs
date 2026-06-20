using Mapcars.Application.Riders.Interfaces;
using Mapcars.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Mapcars.Infrastructure.Persistence.Repositories;

public class RiderRepository : GenericRepository<Rider>, IRiderRepository
{
    public RiderRepository(AppDbContext context) : base(context) { }

    public Task<bool> EmailExistsAsync(string email, CancellationToken ct = default)
        => Set.AnyAsync(r => r.Email == email, ct);

    public Task<Rider?> FindByEmailAsync(string email, CancellationToken ct = default)
        => Set.FirstOrDefaultAsync(r => r.Email == email.ToLowerInvariant().Trim(), ct);

    public Task<Rider?> FindByPhoneAsync(string phone, CancellationToken ct = default)
        => Set.FirstOrDefaultAsync(r => r.PhoneNumber == phone, ct);

    public Task<Rider?> FindByGoogleSubAsync(string googleSub, CancellationToken ct = default)
        => Set.FirstOrDefaultAsync(r => r.GoogleSub == googleSub, ct);

    public Task<bool> PhoneExistsAsync(string phone, CancellationToken ct = default)
        => Set.AnyAsync(r => r.PhoneNumber == phone, ct);
}
