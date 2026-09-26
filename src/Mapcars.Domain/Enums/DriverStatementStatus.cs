namespace Mapcars.Domain.Enums;

/// <summary>Persisted as the member name (database/041 CHECK constraint).</summary>
public enum DriverStatementStatus
{
    /// <summary>Built from a closed week; the numbers are final.</summary>
    Issued,

    /// <summary>The net was paid out (or the commission invoice paid). Set by the payout/invoice step.</summary>
    Settled,
}
