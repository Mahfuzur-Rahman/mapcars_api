using Mapcars.Application.Statements.Interfaces;
using Mapcars.Domain.Entities;
using Mapcars.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Mapcars.Infrastructure.Persistence.Repositories;

public class DriverStatementRepository(AppDbContext db) : IDriverStatementRepository
{
    public async Task<IReadOnlySet<Guid>> DriversWithStatementAsync(DateTime periodStartUtc, CancellationToken ct = default)
        => (await db.DriverStatements.AsNoTracking()
                .Where(s => s.PeriodStartUtc == periodStartUtc)
                .Select(s => s.DriverId)
                .ToListAsync(ct))
            .ToHashSet();

    public async Task<IReadOnlyList<Trip>> ListUnstatedCompletedTripsAsync(
        DateTime fromUtc, DateTime toUtc, CancellationToken ct = default)
        => await db.Trips.AsNoTracking()
            .Where(t => t.Status == TripStatus.Completed
                        && t.DriverId != null
                        && t.CompletedAtUtc >= fromUtc
                        && t.CompletedAtUtc < toUtc
                        && !db.DriverStatementLines.Any(l => l.TripId == t.Id))
            .ToListAsync(ct);

    public async Task<bool> TryAddAsync(DriverStatement statement, CancellationToken ct = default)
    {
        db.DriverStatements.Add(statement);
        try
        {
            await db.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // Two instances built the same week at once; the unique indexes let
            // exactly one win. Detach the loser's copy so the context stays usable.
            db.Entry(statement).State = EntityState.Detached;
            foreach (var line in statement.Lines) db.Entry(line).State = EntityState.Detached;
            return false;
        }
    }

    public async Task<IReadOnlyList<DriverStatement>> ListForDriverAsync(Guid driverId, int take, CancellationToken ct = default)
        => await db.DriverStatements.AsNoTracking()
            .Where(s => s.DriverId == driverId)
            .OrderByDescending(s => s.PeriodStartUtc)
            .Take(take)
            .ToListAsync(ct);

    public Task<DriverStatement?> GetWithLinesAsync(Guid statementId, CancellationToken ct = default)
        => db.DriverStatements.AsNoTracking()
            .Include(s => s.Lines)
            .Include(s => s.Driver)
            .FirstOrDefaultAsync(s => s.Id == statementId, ct);

    public async Task<IReadOnlyList<DriverStatement>> ListAsync(
        DateTime? periodStartUtc, int skip, int take, CancellationToken ct = default)
    {
        var q = db.DriverStatements.AsNoTracking().Include(s => s.Driver).AsQueryable();
        if (periodStartUtc.HasValue) q = q.Where(s => s.PeriodStartUtc == periodStartUtc.Value);
        return await q
            .OrderByDescending(s => s.PeriodStartUtc)
            .ThenByDescending(s => s.NetPence)
            .Skip(skip)
            .Take(take)
            .ToListAsync(ct);
    }
}
