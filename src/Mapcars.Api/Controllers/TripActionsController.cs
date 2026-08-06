using System.Security.Claims;
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

    public TripActionsController(ITripService trips) => _trips = trips;

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
