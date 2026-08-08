using System.Security.Claims;
using Mapcars.Application.Trips.Dtos;
using Mapcars.Application.Trips.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mapcars.Api.Controllers;

/// <summary>
/// Driver-side trip lifecycle: discover open requests and accept/arrive/start/
/// complete one. Every endpoint here requires a driver an admin has approved
/// and who is currently online — an unapproved driver can't see the board at
/// all. <see cref="Available"/> is a bare unfiltered list of open requests;
/// <see cref="AvailableNearby"/> is the geo-filtered board the app polls.
/// </summary>
[ApiController]
[Route("api/v1/trips")]
[Authorize(Roles = "driver")]
public class DriverTripsController : ControllerBase
{
    private readonly ITripService _trips;

    public DriverTripsController(ITripService trips) => _trips = trips;

    /// <summary>All unassigned, still-requested trips (the full broadcast board).</summary>
    [HttpGet("available")]
    [ProducesResponseType(typeof(IReadOnlyList<TripResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Available(CancellationToken ct)
    {
        if (!TryGetDriverId(out var driverId)) return Unauthorized();
        return Ok(await _trips.ListAvailableAsync(driverId, ct));
    }

    /// <summary>Open requests near the driver (their board), nearest first.</summary>
    [HttpGet("available/nearby")]
    [ProducesResponseType(typeof(IReadOnlyList<TripResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AvailableNearby(
        [FromQuery] double lat,
        [FromQuery] double lng,
        [FromQuery] double? radiusMeters,
        CancellationToken ct)
    {
        if (!TryGetDriverId(out var driverId)) return Unauthorized();
        return Ok(await _trips.ListAvailableNearbyAsync(driverId, lat, lng, radiusMeters ?? 10_000, ct));
    }

    /// <summary>The authenticated driver's own trips.</summary>
    [HttpGet("mine")]
    [ProducesResponseType(typeof(IReadOnlyList<TripResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Mine(CancellationToken ct)
    {
        if (!TryGetDriverId(out var driverId)) return Unauthorized();
        return Ok(await _trips.ListForDriverAsync(driverId, ct));
    }

    [HttpPost("{id:guid}/accept")]
    [ProducesResponseType(typeof(TripResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Accept(Guid id, CancellationToken ct)
    {
        if (!TryGetDriverId(out var driverId)) return Unauthorized();
        return Ok(await _trips.AcceptAsync(driverId, id, ct));
    }

    [HttpPost("{id:guid}/arrive")]
    [ProducesResponseType(typeof(TripResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Arrive(Guid id, CancellationToken ct)
    {
        if (!TryGetDriverId(out var driverId)) return Unauthorized();
        return Ok(await _trips.ArriveAsync(driverId, id, ct));
    }

    [HttpPost("{id:guid}/start")]
    [ProducesResponseType(typeof(TripResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Start(Guid id, CancellationToken ct)
    {
        if (!TryGetDriverId(out var driverId)) return Unauthorized();
        return Ok(await _trips.StartAsync(driverId, id, ct));
    }

    [HttpPost("{id:guid}/complete")]
    [ProducesResponseType(typeof(TripResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Complete(Guid id, CancellationToken ct)
    {
        if (!TryGetDriverId(out var driverId)) return Unauthorized();
        return Ok(await _trips.CompleteAsync(driverId, id, ct));
    }

    private bool TryGetDriverId(out Guid driverId)
        => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out driverId);
}
