using System.Security.Claims;
using Mapcars.Application.Ratings.Dtos;
using Mapcars.Application.Ratings.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mapcars.Api.Controllers;

/// <summary>
/// Rider&lt;-&gt;driver ratings for a completed trip. Either participant may submit
/// one rating (in their direction) and list both ratings for the trip.
/// </summary>
[ApiController]
[Route("api/v1/trips/{tripId:guid}/ratings")]
[Authorize]
public class RatingsController : ControllerBase
{
    private readonly IRatingService _ratings;

    public RatingsController(IRatingService ratings) => _ratings = ratings;

    [HttpPost]
    [ProducesResponseType(typeof(RatingResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Submit(Guid tripId, [FromBody] SubmitRatingRequest request, CancellationToken ct)
    {
        var (userType, userId) = CurrentUser();
        if (userId is null) return Unauthorized();

        var response = await _ratings.SubmitAsync(userType, userId.Value, tripId, request, ct);
        return StatusCode(StatusCodes.Status201Created, response);
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<RatingResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> List(Guid tripId, CancellationToken ct)
    {
        var (userType, userId) = CurrentUser();
        if (userId is null) return Unauthorized();

        return Ok(await _ratings.ListForTripAsync(userType, userId.Value, tripId, ct));
    }

    private (string UserType, Guid? UserId) CurrentUser()
    {
        var userType = User.FindFirstValue("user_type") ?? string.Empty;
        var idStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return (userType, Guid.TryParse(idStr, out var id) ? id : null);
    }
}
