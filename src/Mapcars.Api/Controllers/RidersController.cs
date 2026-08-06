using Mapcars.Application.Riders.Dtos;
using Mapcars.Application.Riders.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mapcars.Api.Controllers;

/// <summary>
/// Thin presentation layer — translates HTTP to/from the IRiderService.
/// No business logic here. This is the template for every feature controller.
/// Admin-only: riders sign up/manage their own profile via RiderAuthController,
/// not this controller — this is the admin-portal read surface (Rider List/Detail).
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Authorize(Roles = "SuperAdmin,Admin")]
public class RidersController : ControllerBase
{
    private readonly IRiderService _riders;

    public RidersController(IRiderService riders) => _riders = riders;

    [HttpPost]
    [ProducesResponseType(typeof(RiderResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<RiderResponse>> Create(
        CreateRiderRequest request, CancellationToken ct)
    {
        var rider = await _riders.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = rider.Id }, rider);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(RiderResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RiderResponse>> GetById(Guid id, CancellationToken ct)
        => Ok(await _riders.GetByIdAsync(id, ct));

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<RiderResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<RiderResponse>>> List(CancellationToken ct)
        => Ok(await _riders.ListAsync(ct));
}
