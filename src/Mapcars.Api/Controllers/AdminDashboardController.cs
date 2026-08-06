using Mapcars.Application.Admins.Dtos;
using Mapcars.Application.Admins.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mapcars.Api.Controllers;

/// <summary>
/// Admin-portal reporting: dashboard headline stats, trip history, and the live
/// map (active trips + online drivers). Read-only; SuperAdmin or Admin.
/// </summary>
[ApiController]
[Route("api/v1/admin")]
[Authorize(Roles = "SuperAdmin,Admin")]
public class AdminDashboardController(IAdminDashboardService dashboard) : ControllerBase
{
    /// <summary>Headline counts for the dashboard cards.</summary>
    [HttpGet("stats")]
    [ProducesResponseType(typeof(AdminStatsResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Stats(CancellationToken ct)
        => Ok(await dashboard.GetStatsAsync(ct));

    /// <summary>Paged trip history, optionally filtered by <paramref name="status"/> (a TripStatus name).</summary>
    [HttpGet("trips")]
    [ProducesResponseType(typeof(IReadOnlyList<AdminTripListItem>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Trips(
        [FromQuery] string? status,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 50,
        CancellationToken ct = default)
        => Ok(await dashboard.ListTripsAsync(status, skip, take, ct));

    /// <summary>Live map payload: in-flight trips + currently-online drivers.</summary>
    [HttpGet("live")]
    [ProducesResponseType(typeof(AdminLiveResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Live(CancellationToken ct)
        => Ok(await dashboard.GetLiveAsync(ct));
}
