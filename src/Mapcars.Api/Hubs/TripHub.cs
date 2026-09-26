using System.Security.Claims;
using Mapcars.Application.Common.Exceptions;
using Mapcars.Application.Trips.Interfaces;
using Mapcars.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Mapcars.Api.Hubs;

/// <summary>
/// Realtime channel for a live trip. A client (customer or driver) connects with its
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
    /// Subscribes to a trip's group — only if the caller is that trip's customer
    /// or its assigned driver (silently refuses otherwise, same as a 404 would
    /// on the REST side: don't confirm the trip even exists to a non-party).
    /// </summary>
    public async Task JoinTrip(string tripId)
    {
        if (!Guid.TryParse(tripId, out var id)) return;

        var userType = UserTypes.Canonical(Context.User?.FindFirstValue("user_type"));
        var userIdStr = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdStr, out var userId)) return;

        try
        {
            await _trips.GetForUserAsync(userType, userId, id, Context.ConnectionAborted);
        }
        catch (NotFoundException)
        {
            return; // not this trip's customer or driver
        }

        // Two groups: the shared one for events both parties see identically
        // (location, chat, tripExpiring), and a per-party one for tripUpdated,
        // whose payment fields differ by audience. The server picks the party
        // from the token, so a client can't ask to be put in the other one.
        await Groups.AddToGroupAsync(Context.ConnectionId, GroupFor(tripId));
        await Groups.AddToGroupAsync(Context.ConnectionId,
            UserTypes.IsDriver(userType) ? DriverTripGroupFor(tripId) : CustomerGroupFor(tripId));
    }

    public async Task LeaveTrip(string tripId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupFor(tripId));
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, CustomerGroupFor(tripId));
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, DriverTripGroupFor(tripId));
    }

    /// <summary>The shared SignalR group for a trip — both parties. Shared with the notifier.</summary>
    public static string GroupFor(string tripId) => $"trip:{tripId}";

    /// <summary>The trip's customer only.</summary>
    public static string CustomerGroupFor(string tripId) => $"trip:{tripId}:customer";

    /// <summary>The trip's assigned driver only.</summary>
    public static string DriverTripGroupFor(string tripId) => $"trip:{tripId}:driver";

    /// <summary>A driver's personal group (for offers targeted at them).</summary>
    public static string DriverGroupFor(string driverId) => $"driver:{driverId}";
}
