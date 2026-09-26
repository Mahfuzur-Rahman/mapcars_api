using System.Security.Claims;
using Mapcars.Application.SavedPlaces.Dtos;
using Mapcars.Application.SavedPlaces.Interfaces;
using Mapcars.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mapcars.Api.Controllers;

/// <summary>
/// The authenticated customer's saved places (Home/Work/custom). Customer-scoped
/// only — the customer id comes from the JWT, never from the client, so a customer
/// can only ever read or change their own saved places.
/// </summary>
[ApiController]
[Route("api/v1/saved-places")]
[Authorize(Roles = UserTypes.CustomerRoles)]
public class SavedPlacesController : ControllerBase
{
    private readonly ISavedPlaceService _places;

    public SavedPlacesController(ISavedPlaceService places) => _places = places;

    /// <summary>List the authenticated customer's saved places.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<SavedPlaceResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        if (!TryGetCustomerId(out var customerId)) return Unauthorized();
        return Ok(await _places.ListForCustomerAsync(customerId, ct));
    }

    /// <summary>Add a new saved place for the authenticated customer.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(SavedPlaceResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] UpsertSavedPlaceRequest request, CancellationToken ct)
    {
        if (!TryGetCustomerId(out var customerId)) return Unauthorized();
        var response = await _places.CreateAsync(customerId, request, ct);
        return StatusCode(StatusCodes.Status201Created, response);
    }

    /// <summary>Update one of the authenticated customer's own saved places.</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(SavedPlaceResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpsertSavedPlaceRequest request, CancellationToken ct)
    {
        if (!TryGetCustomerId(out var customerId)) return Unauthorized();
        return Ok(await _places.UpdateAsync(customerId, id, request, ct));
    }

    /// <summary>Delete one of the authenticated customer's own saved places.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        if (!TryGetCustomerId(out var customerId)) return Unauthorized();
        await _places.DeleteAsync(customerId, id, ct);
        return NoContent();
    }

    private bool TryGetCustomerId(out Guid customerId)
        => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out customerId);
}
