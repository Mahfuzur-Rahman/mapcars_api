namespace Mapcars.Application.Statements.Dtos;

/// <summary>
/// A weekly statement. Money is integer pence (new fields are pence from day
/// one — TODO_PAYMENTS.md). <c>NetPence</c> &gt; 0: Mapcars pays the driver;
/// &lt; 0: the driver owes Mapcars commission on cash fares.
/// </summary>
public record DriverStatementSummary(
    Guid Id,
    Guid DriverId,
    string? DriverName,
    DateTime PeriodStartUtc,
    DateTime PeriodEndUtc,
    int TripCount,
    int GrossFaresPence,
    int TipsPence,
    int CommissionPence,
    int DriverEarningsPence,
    int CardPayablePence,
    int CashCommissionOwedPence,
    int NetPence,
    string Status,
    DateTime CreatedAtUtc);

public record DriverStatementLineResponse(
    Guid TripId,
    DateTime CompletedAtUtc,
    string PaymentMethod,
    int FarePence,
    int TipPence,
    int CommissionPence,
    int DriverEarningsPence,
    int AmountPence);

public record DriverStatementDetail(
    DriverStatementSummary Summary,
    IReadOnlyList<DriverStatementLineResponse> Lines);
