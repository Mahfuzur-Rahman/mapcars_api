using Mapcars.Application.Statements.Dtos;

namespace Mapcars.Application.Statements.Interfaces;

public interface IDriverStatementService
{
    /// <summary>
    /// Builds any missing statements for recently closed weeks. Idempotent: safe to
    /// run every hour, and after downtime it catches up. Returns how many it created.
    /// </summary>
    Task<int> BuildRecentAsync(DateTime nowUtc, CancellationToken ct = default);

    Task<IReadOnlyList<DriverStatementSummary>> ListMineAsync(Guid driverId, CancellationToken ct = default);

    /// <summary>A driver's own statement; NotFound for anyone else's.</summary>
    Task<DriverStatementDetail> GetMineAsync(Guid driverId, Guid statementId, CancellationToken ct = default);

    Task<IReadOnlyList<DriverStatementSummary>> AdminListAsync(DateTime? periodStartUtc, int skip, int take, CancellationToken ct = default);

    Task<DriverStatementDetail> AdminGetAsync(Guid statementId, CancellationToken ct = default);
}
