namespace Mapcars.Application.Trips.Interfaces;

/// <summary>Sends the customer's receipt and the driver's earnings summary for a completed trip.</summary>
public interface ITripCompletionEmailer
{
    Task SendAsync(Guid tripId, CancellationToken ct = default);
}
