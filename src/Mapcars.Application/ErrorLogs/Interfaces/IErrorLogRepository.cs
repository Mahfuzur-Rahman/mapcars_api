using Mapcars.Domain.Entities;
using Mapcars.Domain.Enums;

namespace Mapcars.Application.ErrorLogs.Interfaces;

/// <summary>Persistence for <see cref="ErrorLog"/> — append + admin reads.</summary>
public interface IErrorLogRepository
{
    /// <summary>
    /// Writes one entry and commits immediately. Deliberately self-contained
    /// (its own SaveChanges) so logging an error can't be rolled back with, or
    /// accidentally commit, whatever half-finished work failed around it.
    /// </summary>
    Task AddAsync(ErrorLog log, CancellationToken ct = default);

    Task<(IReadOnlyList<ErrorLog> Items, int Total)> ListAsync(
        ErrorSource? source,
        ErrorLevel? level,
        bool? isResolved,
        string? search,
        int page,
        int pageSize,
        CancellationToken ct = default);

    Task<ErrorLog?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<(int Total, int Unresolved, int LastDay, int Errors, int Warnings)> SummaryAsync(
        CancellationToken ct = default);

    /// <summary>Marks one entry resolved/unresolved. Returns false if it's gone.</summary>
    Task<bool> SetResolvedAsync(Guid id, bool resolved, Guid? adminId, CancellationToken ct = default);

    /// <summary>Deletes entries older than <paramref name="olderThanUtc"/>; returns the count.</summary>
    Task<int> PurgeAsync(DateTime olderThanUtc, CancellationToken ct = default);
}
