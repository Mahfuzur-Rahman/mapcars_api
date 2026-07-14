using System.Security.Claims;
using Mapcars.Application.Pricing.Dtos;
using Mapcars.Application.Pricing.Interfaces;
using Mapcars.Application.Trips.Dtos;
using Mapcars.Application.Trips.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mapcars.Api.Controllers;

/// <summary>
/// Rider trips: history, an anonymous fare quote, and booking. Rider-scoped
/// actions require a rider token (see [Authorize(Roles = "rider")]); quoting is
/// open so the choose-ride screen can price a route before/without sign-in.
/// </summary>
[ApiController]
[Route("api/v1/trips")]
[Authorize(Roles = "rider")]
public class TripsController : ControllerBase
{
    private readonly ITripService _trips;
    private readonly IPricingService _pricing;

    public TripsController(ITripService trips, IPricingService pricing)
    {
        _trips = trips;
        _pricing = pricing;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<TripResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<TripResponse>>> List(CancellationToken ct)
    {
        if (!TryGetRiderId(out var riderId)) return Unauthorized();
        return Ok(await _trips.ListForRiderAsync(riderId, ct));
    }

    /// <summary>Price every tier for a route. Open — no fare is charged here.</summary>
    [HttpPost("quote")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(FareQuoteResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<FareQuoteResponse>> Quote([FromBody] FareQuoteRequest req, CancellationToken ct)
        => Ok(await _pricing.QuoteAsync(req, ct));

    /// <summary>Book a trip. The fare is priced authoritatively from the fare chart.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(TripResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<TripResponse>> Create([FromBody] CreateTripRequest req, CancellationToken ct)
    {
        if (!TryGetRiderId(out var riderId)) return Unauthorized();
        var trip = await _trips.CreateAsync(riderId, req, ct);
        return CreatedAtAction(nameof(List), null, trip);
    }

    private bool TryGetRiderId(out Guid riderId)
        => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out riderId);
}
