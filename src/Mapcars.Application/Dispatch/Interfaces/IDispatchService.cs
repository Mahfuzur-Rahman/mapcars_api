using Mapcars.Domain.Entities;

namespace Mapcars.Application.Dispatch.Interfaces;

/// <summary>
/// Why a request is coming off the boards. The driver app renders the two
/// differently, and it should: "someone beat you to it" is a race you lost,
/// while "nobody took it" is information about the market you're sitting in.
/// </summary>
public enum DispatchWithdrawReason
{
    /// <summary>Another driver accepted it, or the customer cancelled it.</summary>
    Taken,

    /// <summary>Its search window ran out with nobody accepting.</summary>
    Expired
}

/// <summary>
/// Matching: broadcast marketplace. A new open request is pushed to all nearby
/// online drivers, who see it on their board (with fare + tip) and race to accept
/// — first-come wins (the atomic accept guards against double-assignment).
/// </summary>
public interface IDispatchService
{
    /// <summary>
    /// Push an open trip to every eligible driver within its current reach.
    /// <paramref name="radiusMeters"/> defaults to <see cref="DispatchRadius"/>
    /// for the trip's age — so booking pushes to the inner ring, and the
    /// escalation sweeper re-pushes at the wider ones as the request ages.
    /// </summary>
    Task BroadcastAsync(Trip trip, double? radiusMeters = null, CancellationToken ct = default);

    /// <summary>
    /// Tell every driver who could have seen this request that it's no longer
    /// open — call once a trip leaves the open board (accepted, or cancelled
    /// before anyone accepted) so it drops off other drivers' boards instead of
    /// lingering until their next poll.
    ///
    /// Deliberately sweeps <see cref="DispatchRadius.MaxMeters"/>, not the
    /// trip's current reach: a request that escalated outward was shown to
    /// drivers who may since have moved, and a driver who is never told it was
    /// taken keeps a dead card on their board that 400s when they tap it.
    /// </summary>
    Task WithdrawAsync(
        Trip trip,
        DispatchWithdrawReason reason = DispatchWithdrawReason.Taken,
        CancellationToken ct = default);
}
