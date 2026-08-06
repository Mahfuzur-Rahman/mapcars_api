using System.Security.Claims;
using Mapcars.Application.Geo.Dtos;
using Mapcars.Application.Geo.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mapcars.Api.Controllers;

/// <summary>
/// Live driver location (Redis GEO hot path). Drivers push their position while
/// online; riders query nearby drivers for the "cars near you" map and matching.
/// </summary>
[ApiController]
[Route("api/v1/drivers")]
public class DriverLocationController : ControllerBase
{
    private readonly IDriverLocationService _locations;

    public DriverLocationController(IDriverLocationService locations) => _locations = locations;

    /// <summary>Push the calling driver's current position (called every few seconds while online).</summary>
    [HttpPut("location")]
    [Authorize(Roles = "driver")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Update([FromBody] UpdateDriverLocationRequest req, CancellationToken ct)
    {
        if (!TryGetDriverId(out var driverId)) return Unauthorized();
        await _locations.UpdateAsync(driverId, req, ct);
        return NoContent();
    }

    /// <summary>Remove the calling driver from the live pool (going offline).</summary>
    [HttpDelete("location")]
    [Authorize(Roles = "driver")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Remove(CancellationToken ct)
    {
        if (!TryGetDriverId(out var driverId)) return Unauthorized();
        await _locations.GoOfflineAsync(driverId, ct);
        return NoContent();
    }

    /// <summary>Online drivers near a point, nearest first.</summary>
    [HttpGet("nearby")]
    [Authorize(Roles = "rider")]
    [ProducesResponseType(typeof(IReadOnlyList<NearbyDriverResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<NearbyDriverResponse>>> Nearby(
        [FromQuery] double lat,
        [FromQuery] double lng,
        [FromQuery] double? radiusMeters,
        [FromQuery] int? limit,
        CancellationToken ct)
        => Ok(await _locations.NearbyAsync(lat, lng, radiusMeters, limit, ct));

    private bool TryGetDriverId(out Guid driverId)
        => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out driverId);
}
