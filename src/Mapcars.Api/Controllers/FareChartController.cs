using Mapcars.Application.Pricing.Interfaces;
using Mapcars.Application.Pricing.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mapcars.Api.Controllers;

/// <summary>
/// The fare chart — the pricing config every client reads to compute an instant
/// local estimate. Reading is public (prices are shown to riders anyway); editing
/// is SuperAdmin-only and publishes a new version (Postgres + Redis + invalidation).
/// </summary>
[ApiController]
[Route("api/v1/fare-chart")]
public class FareChartController : ControllerBase
{
    private readonly IPricingService _pricing;

    public FareChartController(IPricingService pricing) => _pricing = pricing;

    /// <summary>The current fare chart.</summary>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(FareChart), StatusCodes.Status200OK)]
    public async Task<ActionResult<FareChart>> Get(CancellationToken ct)
        => Ok(await _pricing.GetChartAsync(ct));

    /// <summary>Publish a new fare chart. Returns it with its new version.</summary>
    [HttpPut]
    [Authorize(Roles = "SuperAdmin")]
    [ProducesResponseType(typeof(FareChart), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<FareChart>> Update([FromBody] FareChart chart, CancellationToken ct)
        => Ok(await _pricing.UpdateChartAsync(chart, ct));
}
