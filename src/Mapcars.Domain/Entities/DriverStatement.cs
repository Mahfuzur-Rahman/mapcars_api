using Mapcars.Domain.Common;
using Mapcars.Domain.Enums;

namespace Mapcars.Domain.Entities;

/// <summary>
/// A driver's money for one UK week. All amounts are integer pence.
/// <see cref="NetPence"/> is signed: positive means the platform owes the driver,
/// negative means the driver owes the platform (commission on cash fares).
/// See database/041 for the line-sign rules.
/// </summary>
public class DriverStatement : BaseEntity
{
    public Guid DriverId { get; set; }
    public Driver? Driver { get; set; }

    /// <summary>Monday 00:00 Europe/London, as UTC. Inclusive.</summary>
    public DateTime PeriodStartUtc { get; set; }

    /// <summary>The following Monday 00:00 Europe/London, as UTC. Exclusive.</summary>
    public DateTime PeriodEndUtc { get; set; }

    public int TripCount { get; set; }
    public int GrossFaresPence { get; set; }
    public int TipsPence { get; set; }
    public int CommissionPence { get; set; }
    public int DriverEarningsPence { get; set; }
    public int CardPayablePence { get; set; }
    public int CashCommissionOwedPence { get; set; }
    public int NetPence { get; set; }

    public DriverStatementStatus Status { get; set; } = DriverStatementStatus.Issued;

    public List<DriverStatementLine> Lines { get; set; } = [];
}

/// <summary>One completed trip on a statement. <see cref="AmountPence"/> is platform → driver, signed.</summary>
public class DriverStatementLine : BaseEntity
{
    public Guid StatementId { get; set; }
    public DriverStatement? Statement { get; set; }

    public Guid TripId { get; set; }
    public DateTime CompletedAtUtc { get; set; }
    public PaymentMethod PaymentMethod { get; set; }
    public int FarePence { get; set; }
    public int TipPence { get; set; }
    public int CommissionPence { get; set; }
    public int DriverEarningsPence { get; set; }
    public int AmountPence { get; set; }
}
