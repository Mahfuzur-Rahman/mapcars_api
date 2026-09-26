using Mapcars.Application.Common;
using Mapcars.Domain.Entities;
using Mapcars.Domain.Enums;

namespace Mapcars.Application.Statements;

/// <summary>
/// Turns a driver's completed trips for one week into a statement. Pure: no I/O,
/// so the money rules are tested directly.
/// </summary>
public static class StatementBuilder
{
    /// <param name="trips">This driver's trips completed inside <paramref name="period"/>.</param>
    public static DriverStatement Build(Guid driverId, StatementPeriod period, IEnumerable<Trip> trips)
    {
        var statement = new DriverStatement
        {
            DriverId = driverId,
            PeriodStartUtc = period.StartUtc,
            PeriodEndUtc = period.EndUtc,
        };

        foreach (var trip in trips.OrderBy(t => t.CompletedAtUtc))
        {
            var line = LineFor(trip);
            line.StatementId = statement.Id;
            statement.Lines.Add(line);

            statement.TripCount++;
            statement.GrossFaresPence += line.FarePence;
            statement.TipsPence += line.TipPence;
            statement.CommissionPence += line.CommissionPence;
            statement.DriverEarningsPence += line.DriverEarningsPence;

            if (line.PaymentMethod == PaymentMethod.Card)
                statement.CardPayablePence += line.AmountPence;
            else
                statement.CashCommissionOwedPence += line.CommissionPence;

            statement.NetPence += line.AmountPence;
        }

        return statement;
    }

    /// <summary>
    /// The sign rule, stated once. Card: the platform collected the fare (or
    /// absorbed its failure), so it owes the driver their share and the tip.
    /// Cash: the driver holds the fare and the tip, so they owe the commission.
    /// </summary>
    public static DriverStatementLine LineFor(Trip trip)
    {
        var fare = Money.ToPence(trip.FareAmount ?? 0m);
        var tip = Money.ToPence(trip.TipAmount);
        var commission = Money.ToPence(trip.PlatformFeeAmount ?? 0m);
        var earnings = Money.ToPence(trip.DriverEarnings ?? 0m);

        return new DriverStatementLine
        {
            TripId = trip.Id,
            CompletedAtUtc = trip.CompletedAtUtc ?? trip.CreatedAtUtc,
            PaymentMethod = trip.PaymentMethod,
            FarePence = fare,
            TipPence = tip,
            CommissionPence = commission,
            DriverEarningsPence = earnings,
            AmountPence = trip.PaymentMethod == PaymentMethod.Card ? earnings + tip : -commission,
        };
    }
}
