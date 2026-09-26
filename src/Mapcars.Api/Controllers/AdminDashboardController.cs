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

    /// <summary>
    /// Money on completed trips: fare, commission, driver share and the customer's
    /// real payment state. Filters: <paramref name="method"/> (Cash|Card),
    /// <paramref name="paymentStatus"/> (a PaymentStatus name), and a completion
    /// window in UTC. Totals cover the whole filtered range.
    /// </summary>
    [HttpGet("transactions")]
    [ProducesResponseType(typeof(AdminTransactionsPage), StatusCodes.Status200OK)]
    public async Task<IActionResult> Transactions(
        [FromQuery] string? method,
        [FromQuery] string? paymentStatus,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 50,
        CancellationToken ct = default)
        => Ok(await dashboard.ListTransactionsAsync(method, paymentStatus, from, to, skip, take, ct));

    /// <summary>Driver payouts, optionally filtered by <paramref name="status"/> (a PayoutStatus name).</summary>
    [HttpGet("payouts")]
    [ProducesResponseType(typeof(AdminPayoutsPage), StatusCodes.Status200OK)]
    public async Task<IActionResult> Payouts(
        [FromQuery] string? status,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 50,
        CancellationToken ct = default)
        => Ok(await dashboard.ListPayoutsAsync(status, skip, take, ct));

    /// <summary>Live map payload: in-flight trips + currently-online drivers.</summary>
    [HttpGet("live")]
    [ProducesResponseType(typeof(AdminLiveResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Live(CancellationToken ct)
        => Ok(await dashboard.GetLiveAsync(ct));
}
