using Mapcars.Application.Auth.Interfaces;
using Mapcars.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Mapcars.Infrastructure.Persistence.Repositories;

/// <summary>EF Core store for refresh tokens. SaveChanges is the caller's (unit of work).</summary>
public class RefreshTokenRepository(AppDbContext db) : IRefreshTokenRepository
{
    public async Task AddAsync(RefreshToken token, CancellationToken ct = default)
        => await db.RefreshTokens.AddAsync(token, ct);

    public Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken ct = default)
        => db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == tokenHash, ct);

    public async Task<IReadOnlyList<RefreshToken>> ListActiveForUserAsync(
        Guid userId, string userType, CancellationToken ct = default)
        => await db.RefreshTokens
            .Where(t => t.UserId == userId
                     && t.UserType == userType
                     && t.RevokedAtUtc == null
                     && t.ExpiresAtUtc > DateTime.UtcNow)
            .ToListAsync(ct);

    public async Task<int> PurgeExpiredAsync(int olderThanDays, CancellationToken ct = default)
    {
        var cutoff = DateTime.UtcNow.AddDays(-olderThanDays);
        return await db.RefreshTokens
            .Where(t => t.ExpiresAtUtc < cutoff)
            .ExecuteDeleteAsync(ct);
    }
}
