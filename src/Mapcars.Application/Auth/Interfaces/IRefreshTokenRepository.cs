using Mapcars.Domain.Entities;

namespace Mapcars.Application.Auth.Interfaces;

/// <summary>Store for refresh tokens. SaveChanges is the caller's (unit of work).</summary>
public interface IRefreshTokenRepository
{
    Task AddAsync(RefreshToken token, CancellationToken ct = default);

    /// <summary>Looks a token up by its hash. Returns the row whatever its state —
    /// the service needs to see revoked/expired rows to tell "unknown token" from
    /// "reused token", which are very different signals.</summary>
    Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken ct = default);

    /// <summary>Every non-revoked, non-expired token for one user.</summary>
    Task<IReadOnlyList<RefreshToken>> ListActiveForUserAsync(
        Guid userId, string userType, CancellationToken ct = default);

    /// <summary>Deletes rows that expired more than <paramref name="olderThanDays"/>
    /// ago. Housekeeping — a revoked or long-dead token has no audit value.</summary>
    Task<int> PurgeExpiredAsync(int olderThanDays, CancellationToken ct = default);
}
