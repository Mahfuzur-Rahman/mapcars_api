using Mapcars.Domain.Entities;

namespace Mapcars.Application.Dispatch.Interfaces;

/// <summary>
/// Matching: broadcast marketplace. A new open request is pushed to all nearby
/// online drivers, who see it on their board (with fare + tip) and race to accept
/// — first-come wins (the atomic accept guards against double-assignment).
/// </summary>
public interface IDispatchService
{
    /// <summary>Push a newly-booked open trip to every nearby eligible driver.</summary>
    Task BroadcastAsync(Trip trip, CancellationToken ct = default);

    /// <summary>
    /// Tell every driver who could have seen this request (same nearby pool as
    /// <see cref="BroadcastAsync"/>) that it's no longer open — call once a
    /// trip leaves the open board (accepted, or cancelled before anyone
    /// accepted) so it drops off other drivers' boards instead of lingering
    /// until their next poll.
    /// </summary>
    Task WithdrawAsync(Trip trip, CancellationToken ct = default);
}
