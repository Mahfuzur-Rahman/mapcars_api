namespace Mapcars.Application.Trips.Interfaces;

/// <summary>
/// Hands a completed trip's emails to a background worker. Sending inline would
/// put the mail provider's latency — or its outage — into the driver's
/// "Complete trip" tap. Fire-and-forget: a lost receipt is an inconvenience,
/// a stuck completion is not.
/// </summary>
public interface ITripEmailQueue
{
    void EnqueueCompleted(Guid tripId);
}
