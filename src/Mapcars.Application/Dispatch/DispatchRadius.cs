using Mapcars.Domain.Entities;

namespace Mapcars.Application.Dispatch;

/// <summary>
/// How far an open request reaches, as a function of how long it has gone
/// unaccepted. One fixed radius can't serve both cases: 6 miles is right in
/// central London and leaves a suburban booking with nobody in range, while a
/// radius wide enough for the suburbs would put every London job in front of
/// fifty drivers who will never take it (the deadhead to the pickup is unpaid,
/// so distance is a cost the driver eats). So the ring widens instead — nearby
/// drivers get first refusal, and only a job that's actually going begging is
/// shown to drivers further out.
///
/// **This is the single source of truth for that rule.** Both halves of the
/// dispatch board have to agree on it: the push (<c>IDispatchService</c>, and
/// the sweeper that re-broadcasts as the ring widens) and the pull
/// (<c>ITripService.ListAvailableNearbyAsync</c>, which the driver app's
/// watchdog polls). If the pull used a narrower rule than the push, a job
/// pushed to a distant driver would be deleted off their board by their very
/// next poll.
///
/// Constants for now; making them admin-configurable per city is an open TODO.
/// </summary>
public static class DispatchRadius
{
    /// <summary>At booking: ~6.2 miles.</summary>
    public const double InitialMeters = 10_000;

    /// <summary>Once it's gone unaccepted for <see cref="WidenAfter"/>: ~15.5 miles.</summary>
    public const double WideMeters = 25_000;

    /// <summary>The widest it ever goes, after <see cref="MaxAfter"/>: ~25 miles.</summary>
    public const double MaxMeters = 40_000;

    public static readonly TimeSpan WidenAfter = TimeSpan.FromSeconds(60);
    public static readonly TimeSpan MaxAfter = TimeSpan.FromSeconds(120);

    /// <summary>The radius an open request of this age currently reaches.</summary>
    public static double ForAge(TimeSpan age) =>
        age >= MaxAfter ? MaxMeters
        : age >= WidenAfter ? WideMeters
        : InitialMeters;

    /// <summary>
    /// The radius <paramref name="trip"/> currently reaches. Measured from when
    /// it was booked, not from when a given driver came online — a driver going
    /// online now sees a three-minute-old job at the full radius straight away,
    /// which is the point.
    /// </summary>
    public static double For(Trip trip, DateTime nowUtc) => ForAge(nowUtc - trip.CreatedAtUtc);
}
