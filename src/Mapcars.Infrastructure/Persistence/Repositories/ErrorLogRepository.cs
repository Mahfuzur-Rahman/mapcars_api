using Mapcars.Application.ErrorLogs.Interfaces;
using Mapcars.Domain.Entities;
using Mapcars.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Mapcars.Infrastructure.Persistence.Repositories;

/// <summary>
/// Error-log persistence. <see cref="AddAsync"/> commits on its own rather than
/// joining the ambient unit of work: the thing being logged has usually just
/// blown up mid-request, and its transaction is about to be abandoned — an
/// entry that rolls back with it would be worse than useless.
/// </summary>
public class ErrorLogRepository : IErrorLogRepository
{
    private readonly AppDbContext _db;

    public ErrorLogRepository(AppDbContext db) => _db = db;

    public async Task AddAsync(ErrorLog log, CancellationToken ct = default)
    {
        _db.ErrorLogs.Add(log);
        await _db.SaveChangesAsync(ct);
        // Don't leave it tracked — the failing request's own SaveChanges (if it
        // somehow still runs) shouldn't touch this row again.
        _db.Entry(log).State = EntityState.Detached;
    }

    public async Task<(IReadOnlyList<ErrorLog> Items, int Total)> ListAsync(
        ErrorSource? source,
        ErrorLevel? level,
        bool? isResolved,
        string? search,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var query = _db.ErrorLogs.AsNoTracking().AsQueryable();

        if (source is not null) query = query.Where(e => e.Source == source);
        if (level is not null) query = query.Where(e => e.Level == level);
        if (isResolved is not null) query = query.Where(e => e.IsResolved == isResolved);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search}%";
            query = query.Where(e =>
                EF.Functions.ILike(e.Message, pattern) ||
                (e.ExceptionType != null && EF.Functions.ILike(e.ExceptionType, pattern)) ||
                (e.Path != null && EF.Functions.ILike(e.Path, pattern)));
        }

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(e => e.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, total);
    }

    public async Task<ErrorLog?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _db.ErrorLogs.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id, ct);

    public async Task<(int Total, int Unresolved, int LastDay, int Errors, int Warnings)> SummaryAsync(
        CancellationToken ct = default)
    {
        var since = DateTime.UtcNow.AddDays(-1);

        // One round trip for all five counts.
        return await _db.ErrorLogs.AsNoTracking()
            .GroupBy(_ => 1)
            .Select(g => new ValueTuple<int, int, int, int, int>(
                g.Count(),
                g.Count(e => !e.IsResolved),
                g.Count(e => e.CreatedAtUtc >= since),
                g.Count(e => e.Level == ErrorLevel.Error || e.Level == ErrorLevel.Fatal),
                g.Count(e => e.Level == ErrorLevel.Warning)))
            .FirstOrDefaultAsync(ct);
    }

    public async Task<bool> SetResolvedAsync(Guid id, bool resolved, Guid? adminId, CancellationToken ct = default)
    {
        var log = await _db.ErrorLogs.FirstOrDefaultAsync(e => e.Id == id, ct);
        if (log is null) return false;

        log.IsResolved = resolved;
        log.ResolvedAtUtc = resolved ? DateTime.UtcNow : null;
        log.ResolvedByAdminId = resolved ? adminId : null;

        await _db.SaveChangesAsync(ct);
        return true;
    }

    public Task<int> PurgeAsync(DateTime olderThanUtc, CancellationToken ct = default)
        => _db.ErrorLogs.Where(e => e.CreatedAtUtc < olderThanUtc).ExecuteDeleteAsync(ct);
}
