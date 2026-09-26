using Mapcars.Application.Notifications.Interfaces;
using Mapcars.Domain.Constants;
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

    /// <summary>
    /// Tokens for one user, accepting either passenger spelling for the length of
    /// the Customer -> Customer rename.
    ///
    /// <para>
    /// The tolerance matters more here than almost anywhere else, because this
    /// filter fails <b>silently</b>: rows written before the migration still hold
    /// the old value, and if the lookup stops matching them, <c>PushService</c>
    /// simply finds no tokens and returns. Every push to that user stops — no
    /// exception, no error status, nothing in the logs above debug. You would
    /// hear about it from a user, days later.
    /// </para>
    /// </summary>
    public async Task<IReadOnlyList<string>> ListTokensForUserAsync(
        string userType, Guid userId, CancellationToken ct = default)
    {
        // Translates to IN (...), so the (user_type, user_id) index still applies.
        var accepted = UserTypes.IsCustomer(userType)
            ? new[] { UserTypes.Customer, UserTypes.LegacyCustomer }
            : new[] { userType };

        return await db.DeviceTokens
            .Where(t => accepted.Contains(t.UserType) && t.UserId == userId)
            .Select(t => t.Token)
            .ToListAsync(ct);
    }
}
