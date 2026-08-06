using System.Security.Claims;
using Mapcars.Application.Vehicles.Dtos;
using Mapcars.Application.Vehicles.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mapcars.Api.Controllers;

/// <summary>
/// The authenticated driver's own vehicle. Driver-scoped only — the driver id
/// comes from the JWT, never from the client, so a driver can only ever read or
/// change their own vehicle.
/// </summary>
[ApiController]
[Route("api/v1/vehicles")]
[Authorize(Roles = "driver")]
public class VehiclesController : ControllerBase
{
    private readonly IVehicleService _vehicles;

    public VehiclesController(IVehicleService vehicles) => _vehicles = vehicles;

    /// <summary>Get the authenticated driver's vehicle (204 if none registered yet).</summary>
    [HttpGet("me")]
    [ProducesResponseType(typeof(VehicleResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> GetMine(CancellationToken ct)
    {
        if (!TryGetDriverId(out var driverId)) return Unauthorized();

        var vehicle = await _vehicles.GetForDriverAsync(driverId, ct);
        return vehicle is null ? NoContent() : Ok(vehicle);
    }

    /// <summary>Create or replace the authenticated driver's vehicle.</summary>
    [HttpPut("me")]
    [ProducesResponseType(typeof(VehicleResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpsertMine([FromBody] UpsertVehicleRequest request, CancellationToken ct)
    {
        if (!TryGetDriverId(out var driverId)) return Unauthorized();

        return Ok(await _vehicles.UpsertForDriverAsync(driverId, request, ct));
    }

    private bool TryGetDriverId(out Guid driverId)
        => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out driverId);
}
