namespace Mapcars.Application.Statements;

/// <summary>
/// The statement week: Monday 00:00 to the next Monday 00:00, <b>UK local time</b>,
/// held as UTC instants.
///
/// <para>
/// Local, not UTC, because a driver's week is the week on their wall clock. In
/// summer (BST, UTC+1) the week starts at Sunday 23:00 UTC; a UTC-based week
/// would put a driver's Sunday-night shift on the wrong statement. The two
/// weeks that contain a clock change are 167 and 169 hours long — correct, not
/// a bug.
/// </para>
/// </summary>
public readonly record struct StatementPeriod(DateTime StartUtc, DateTime EndUtc)
{
    /// <summary>
    /// How long after a week closes before it is built. A trip completed at
    /// 23:59:59 Sunday commits a moment later; the grace makes sure it is in the
    /// table before its week is snapshotted.
    /// </summary>
    public static readonly TimeSpan BuildGrace = TimeSpan.FromHours(1);

    private static readonly TimeZoneInfo UkZone = TimeZoneInfo.FindSystemTimeZoneById("Europe/London");

    /// <summary>The week containing <paramref name="instantUtc"/>.</summary>
    public static StatementPeriod Containing(DateTime instantUtc)
    {
        var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(instantUtc, DateTimeKind.Utc), UkZone);
        var daysSinceMonday = ((int)local.DayOfWeek + 6) % 7;
        var mondayLocal = local.Date.AddDays(-daysSinceMonday);
        return new StatementPeriod(ToUtc(mondayLocal), ToUtc(mondayLocal.AddDays(7)));
    }

    /// <summary>
    /// The most recent week that has closed and cleared <see cref="BuildGrace"/>,
    /// then the <paramref name="count"/> − 1 weeks before it, newest first. The
    /// job looks back more than one week so a few days of downtime heal themselves.
    /// </summary>
    public static IReadOnlyList<StatementPeriod> RecentlyClosed(DateTime nowUtc, int count)
    {
        var latest = Containing(nowUtc - BuildGrace).Previous();
        var periods = new List<StatementPeriod>(count);
        for (var i = 0; i < count; i++)
        {
            periods.Add(latest);
            latest = latest.Previous();
        }
        return periods;
    }

    public StatementPeriod Previous() => Containing(StartUtc.AddHours(-12));

    /// <summary>Midnight on the UK wall clock. Midnight never falls in a UK clock-change gap (those are at 01:00).</summary>
    private static DateTime ToUtc(DateTime localMidnight)
        => TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(localMidnight, DateTimeKind.Unspecified), UkZone);
}
