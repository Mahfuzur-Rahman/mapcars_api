using System.Security.Claims;
using Mapcars.Application.Geo.Dtos;
using Mapcars.Application.Geo.Interfaces;
using Mapcars.Application.Trips.Dtos;
using Mapcars.Application.Trips.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mapcars.Api.Controllers;

/// <summary>
/// Trip actions valid for either party. Kept separate from the rider-only
/// <see cref="TripsController"/> and driver-only <see cref="DriverTripsController"/>
/// so this one endpoint isn't role-restricted to a single side.
/// </summary>
[ApiController]
[Route("api/v1/trips")]
[Authorize]
public class TripActionsController : ControllerBase
{
    private readonly ITripService _trips;
    private readonly IDriverLocationService _locations;

    public TripActionsController(ITripService trips, IDriverLocationService locations)
    {
        _trips = trips;
        _locations = locations;
    }

    /// <summary>Fetch one trip — the caller must be its rider or assigned driver.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(TripResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var (userType, userId) = CurrentUser();
        if (userId is null) return Unauthorized();
        return Ok(await _trips.GetForUserAsync(userType, userId.Value, id, ct));
    }

    /// <summary>
    /// The assigned driver's last known position for this trip — the cold-start
    /// seed for the rider's tracking map, before the realtime
    /// <c>driverLocation</c> pushes take over. 204 when there's nothing to show
    /// (no driver assigned yet, trip already over, or the driver isn't reporting).
    /// </summary>
    [HttpGet("{id:guid}/driver-location")]
    [ProducesResponseType(typeof(TripDriverLocationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DriverLocation(Guid id, CancellationToken ct)
    {
        var (userType, userId) = CurrentUser();
        if (userId is null) return Unauthorized();

        var position = await _locations.ForTripAsync(userType, userId.Value, id, ct);
        return position is null ? NoContent() : Ok(position);
    }

    /// <summary>Cancel a trip. Callable by the trip's rider or its assigned driver.</summary>
    [HttpPost("{id:guid}/cancel")]
    [ProducesResponseType(typeof(TripResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Cancel(Guid id, [FromBody] CancelTripRequest request, CancellationToken ct)
    {
        var (userType, userId) = CurrentUser();
        if (userId is null) return Unauthorized();

        return Ok(await _trips.CancelAsync(userType, userId.Value, id, request, ct));
    }

    private (string UserType, Guid? UserId) CurrentUser()
    {
        var userType = User.FindFirstValue("user_type") ?? string.Empty;
        var idStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return (userType, Guid.TryParse(idStr, out var id) ? id : null);
    }
}
