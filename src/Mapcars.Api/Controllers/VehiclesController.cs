using System.Security.Claims;
using Mapcars.Application.Vehicles.Dtos;
using Mapcars.Application.Vehicles.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mapcars.Api.Controllers;

/// <summary>
/// The authenticated driver's own vehicle and tier appeals. Driver-scoped only — the driver id
/// comes from the JWT, never from the client.
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

    /// <summary>Submit an appeal to upgrade vehicle tier (with optional photo attachments).</summary>
    [HttpPost("me/appeals")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(35 * 1024 * 1024)]
    [ProducesResponseType(typeof(VehicleTierAppealResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SubmitAppeal(
        [FromForm] string requestedTier,
        [FromForm] string reason,
        [FromForm] List<IFormFile>? photos,
        CancellationToken ct)
    {
        if (!TryGetDriverId(out var driverId)) return Unauthorized();

        List<(Stream Stream, string ContentType, string FileName)>? photoStreams = null;
        if (photos is { Count: > 0 })
        {
            photoStreams = new List<(Stream Stream, string ContentType, string FileName)>();
            foreach (var file in photos)
            {
                if (file.Length > 0)
                {
                    photoStreams.Add((file.OpenReadStream(), file.ContentType, file.FileName));
                }
            }
        }

        var appeal = await _vehicles.SubmitTierAppealAsync(driverId, requestedTier, reason, photoStreams, ct);
        return StatusCode(StatusCodes.Status201Created, appeal);
    }

    /// <summary>Submit appeal via JSON (when no photos are attached).</summary>
    [HttpPost("me/appeals/json")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(VehicleTierAppealResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SubmitAppealJson([FromBody] CreateTierAppealRequest request, CancellationToken ct)
    {
        if (!TryGetDriverId(out var driverId)) return Unauthorized();

        var appeal = await _vehicles.SubmitTierAppealAsync(driverId, request.RequestedTier, request.Reason, null, ct);
        return StatusCode(StatusCodes.Status201Created, appeal);
    }

    /// <summary>List all tier appeals submitted for the driver's vehicle.</summary>
    [HttpGet("me/appeals")]
    [ProducesResponseType(typeof(IReadOnlyList<VehicleTierAppealResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListMyAppeals(CancellationToken ct)
    {
        if (!TryGetDriverId(out var driverId)) return Unauthorized();

        return Ok(await _vehicles.ListAppealsForDriverAsync(driverId, ct));
    }

    /// <summary>Get active pending appeal for the driver's vehicle (204 if none).</summary>
    [HttpGet("me/appeals/active")]
    [ProducesResponseType(typeof(VehicleTierAppealResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> GetActiveAppeal(CancellationToken ct)
    {
        if (!TryGetDriverId(out var driverId)) return Unauthorized();

        var appeal = await _vehicles.GetActiveAppealForDriverAsync(driverId, ct);
        return appeal is null ? NoContent() : Ok(appeal);
    }

    /// <summary>Stream an attached photo from a driver's appeal.</summary>
    [HttpGet("me/appeals/{appealId:guid}/photos/{photoIndex:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAppealPhoto(Guid appealId, int photoIndex, CancellationToken ct)
    {
        if (!TryGetDriverId(out var driverId)) return Unauthorized();

        var file = await _vehicles.GetAppealPhotoContentAsync(driverId, appealId, photoIndex, ct);
        if (file is null) return NotFound();

        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return File(file.Content, file.ContentType);
    }

    private bool TryGetDriverId(out Guid driverId)
        => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out driverId);
}
