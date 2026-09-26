using Mapcars.Application.Common.Exceptions;
using Mapcars.Application.Statements.Dtos;
using Mapcars.Application.Statements.Interfaces;
using Mapcars.Domain.Entities;

namespace Mapcars.Application.Statements.Services;

public class DriverStatementService(IDriverStatementRepository statements) : IDriverStatementService
{
    /// <summary>How many closed weeks each run re-checks — enough to heal a few days of downtime.</summary>
    private const int LookbackWeeks = 4;

    private const int MaxDriverStatements = 104; // two years of weeks

    public async Task<int> BuildRecentAsync(DateTime nowUtc, CancellationToken ct = default)
    {
        var created = 0;
        foreach (var period in StatementPeriod.RecentlyClosed(nowUtc, LookbackWeeks))
        {
            var done = await statements.DriversWithStatementAsync(period.StartUtc, ct);
            var trips = await statements.ListUnstatedCompletedTripsAsync(period.StartUtc, period.EndUtc, ct);

            foreach (var group in trips.GroupBy(t => t.DriverId!.Value))
            {
                // A driver who already has this week's statement keeps it as issued:
                // statements are immutable once built. (A trip that somehow lands in a
                // closed week after the fact is left unstated for an admin to see,
                // never silently re-cut into a document the driver has already read.)
                if (done.Contains(group.Key)) continue;

                var statement = StatementBuilder.Build(group.Key, period, group);
                if (await statements.TryAddAsync(statement, ct)) created++;
            }
        }
        return created;
    }

    public async Task<IReadOnlyList<DriverStatementSummary>> ListMineAsync(Guid driverId, CancellationToken ct = default)
        => (await statements.ListForDriverAsync(driverId, MaxDriverStatements, ct)).Select(Summary).ToList();

    public async Task<DriverStatementDetail> GetMineAsync(Guid driverId, Guid statementId, CancellationToken ct = default)
    {
        var s = await statements.GetWithLinesAsync(statementId, ct);
        // Someone else's statement is "not found", never "forbidden".
        if (s is null || s.DriverId != driverId) throw new NotFoundException("Statement", statementId);
        return Detail(s);
    }

    public async Task<IReadOnlyList<DriverStatementSummary>> AdminListAsync(
        DateTime? periodStartUtc, int skip, int take, CancellationToken ct = default)
        => (await statements.ListAsync(periodStartUtc, Math.Max(0, skip), Math.Clamp(take, 1, 200), ct))
            .Select(Summary).ToList();

    public async Task<DriverStatementDetail> AdminGetAsync(Guid statementId, CancellationToken ct = default)
        => Detail(await statements.GetWithLinesAsync(statementId, ct) ?? throw new NotFoundException("Statement", statementId));

    private static DriverStatementSummary Summary(DriverStatement s) => new(
        s.Id, s.DriverId, s.Driver?.FullName, s.PeriodStartUtc, s.PeriodEndUtc, s.TripCount,
        s.GrossFaresPence, s.TipsPence, s.CommissionPence, s.DriverEarningsPence,
        s.CardPayablePence, s.CashCommissionOwedPence, s.NetPence, s.Status.ToString(), s.CreatedAtUtc);

    private static DriverStatementDetail Detail(DriverStatement s) => new(
        Summary(s),
        s.Lines.OrderBy(l => l.CompletedAtUtc)
            .Select(l => new DriverStatementLineResponse(
                l.TripId, l.CompletedAtUtc, l.PaymentMethod.ToString(), l.FarePence, l.TipPence,
                l.CommissionPence, l.DriverEarningsPence, l.AmountPence))
            .ToList());
}
