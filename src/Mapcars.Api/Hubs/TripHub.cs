using System.Security.Claims;
using Mapcars.Application.Common.Exceptions;
using Mapcars.Application.Trips.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Mapcars.Api.Hubs;

/// <summary>
/// Realtime channel for a live trip. A client (rider or driver) connects with its
/// JWT (passed as the <c>access_token</c> query param — see Program.cs) and calls
/// <see cref="JoinTrip"/> to subscribe to a trip's per-trip group; the server then
/// pushes <c>tripUpdated</c> (and, later, driver-location) events to that group.
/// </summary>
[Authorize]
public class TripHub : Hub
{
    private readonly ITripService _trips;

    public TripHub(ITripService trips) => _trips = trips;

    /// <summary>
    /// Drivers auto-join their personal group on connect so dispatch pushes
    /// (<c>tripAvailable</c>/<c>tripTaken</c>) reach them without an explicit
    /// subscribe.
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        if (string.Equals(Context.User?.FindFirstValue(ClaimTypes.Role), "driver", StringComparison.OrdinalIgnoreCase))
        {
            var driverId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!string.IsNullOrEmpty(driverId))
                await Groups.AddToGroupAsync(Context.ConnectionId, DriverGroupFor(driverId));
        }
        await base.OnConnectedAsync();
    }

    /// <summary>
    /// Subscribes to a trip's group — only if the caller is that trip's rider
    /// or its assigned driver (silently refuses otherwise, same as a 404 would
    /// on the REST side: don't confirm the trip even exists to a non-party).
    /// </summary>
    public async Task JoinTrip(string tripId)
    {
        if (!Guid.TryParse(tripId, out var id)) return;

        var userType = Context.User?.FindFirstValue("user_type") ?? string.Empty;
        var userIdStr = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdStr, out var userId)) return;

        try
        {
            await _trips.GetForUserAsync(userType, userId, id, Context.ConnectionAborted);
        }
        catch (NotFoundException)
        {
            return; // not this trip's rider or driver
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, GroupFor(tripId));
    }

    public Task LeaveTrip(string tripId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupFor(tripId));

    /// <summary>The SignalR group name for a trip. Shared with the notifier.</summary>
    public static string GroupFor(string tripId) => $"trip:{tripId}";

    /// <summary>A driver's personal group (for offers targeted at them).</summary>
    public static string DriverGroupFor(string driverId) => $"driver:{driverId}";
}
