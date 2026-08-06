using Mapcars.Application.Notifications.Interfaces;
using Mapcars.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Mapcars.Infrastructure.Persistence.Repositories;

/// <summary>EF Core store for FCM device tokens. SaveChanges is the caller's (unit of work).</summary>
public class DeviceTokenRepository(AppDbContext db) : IDeviceTokenRepository
{
    public async Task UpsertAsync(DeviceToken token, CancellationToken ct = default)
    {
        var existing = await db.DeviceTokens.FirstOrDefaultAsync(t => t.Token == token.Token, ct);
        if (existing is null)
        {
            await db.DeviceTokens.AddAsync(token, ct);
        }
        else
        {
            // Token moved to (or re-registered by) this owner.
            existing.UserType = token.UserType;
            existing.UserId = token.UserId;
            existing.Platform = token.Platform;
        }
    }

    public async Task RemoveByTokenAsync(string token, CancellationToken ct = default)
    {
        var existing = await db.DeviceTokens.FirstOrDefaultAsync(t => t.Token == token, ct);
        if (existing is not null) db.DeviceTokens.Remove(existing);
    }

    public async Task RemoveTokensAsync(IReadOnlyCollection<string> tokens, CancellationToken ct = default)
    {
        if (tokens.Count == 0) return;
        var rows = await db.DeviceTokens.Where(t => tokens.Contains(t.Token)).ToListAsync(ct);
        db.DeviceTokens.RemoveRange(rows);
    }

    public async Task<IReadOnlyList<string>> ListTokensForUserAsync(
        string userType, Guid userId, CancellationToken ct = default)
        => await db.DeviceTokens
            .Where(t => t.UserType == userType && t.UserId == userId)
            .Select(t => t.Token)
            .ToListAsync(ct);
}
