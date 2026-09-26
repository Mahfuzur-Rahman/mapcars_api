using Mapcars.Domain.Entities;

namespace Mapcars.Application.Statements.Interfaces;

public interface IDriverStatementRepository
{
    /// <summary>Drivers who already have a statement for the week starting at <paramref name="periodStartUtc"/>.</summary>
    Task<IReadOnlySet<Guid>> DriversWithStatementAsync(DateTime periodStartUtc, CancellationToken ct = default);

    /// <summary>
    /// Completed trips with a driver, completed in [<paramref name="fromUtc"/>, <paramref name="toUtc"/>)
    /// and not already on any statement.
    /// </summary>
    Task<IReadOnlyList<Trip>> ListUnstatedCompletedTripsAsync(DateTime fromUtc, DateTime toUtc, CancellationToken ct = default);

    /// <summary>
    /// Inserts the statement and its lines. Returns false if the unique indexes
    /// say another run got there first — the loser discards its copy.
    /// </summary>
    Task<bool> TryAddAsync(DriverStatement statement, CancellationToken ct = default);

    Task<IReadOnlyList<DriverStatement>> ListForDriverAsync(Guid driverId, int take, CancellationToken ct = default);

    /// <summary>With lines. Null if it doesn't exist.</summary>
    Task<DriverStatement?> GetWithLinesAsync(Guid statementId, CancellationToken ct = default);

    /// <summary>Newest first, optionally one week only. Includes the driver for the name.</summary>
    Task<IReadOnlyList<DriverStatement>> ListAsync(DateTime? periodStartUtc, int skip, int take, CancellationToken ct = default);
}
