using System.Security.Claims;
using Mapcars.Application.Pricing.Dtos;
using Mapcars.Application.Pricing.Interfaces;
using Mapcars.Application.Trips.Dtos;
using Mapcars.Application.Trips.Interfaces;
using Mapcars.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mapcars.Api.Controllers;

/// <summary>
/// Customer trips: history, an anonymous fare quote, and booking. Customer-scoped
/// actions require a customer token (see [Authorize(Roles = UserTypes.CustomerRoles)]); quoting is
/// open so the choose-ride screen can price a route before/without sign-in.
/// </summary>
[ApiController]
[Route("api/v1/trips")]
[Authorize(Roles = UserTypes.CustomerRoles)]
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
        if (!TryGetCustomerId(out var customerId)) return Unauthorized();
        return Ok(await _trips.ListForCustomerAsync(customerId, ct));
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
        if (!TryGetCustomerId(out var customerId)) return Unauthorized();
        var trip = await _trips.CreateAsync(customerId, req, ct);
        return CreatedAtAction(nameof(List), null, trip);
    }

    /// <summary>
    /// Keep searching. Gives a still-open request another search window and puts
    /// it back in front of drivers — the customer's answer when nobody has taken it.
    /// </summary>
    [HttpPost("{id:guid}/extend")]
    [ProducesResponseType(typeof(TripResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TripResponse>> Extend(Guid id, CancellationToken ct)
    {
        if (!TryGetCustomerId(out var customerId)) return Unauthorized();
        return Ok(await _trips.ExtendAsync(customerId, id, ct));
    }

    private bool TryGetCustomerId(out Guid customerId)
        => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out customerId);
}
