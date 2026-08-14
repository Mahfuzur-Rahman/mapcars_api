using System.Security.Claims;
using Mapcars.Application.Messages.Dtos;
using Mapcars.Application.Messages.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mapcars.Api.Controllers;

/// <summary>
/// In-trip chat messages. Either participant (rider or driver) may send
/// messages and list the full conversation history for their trip.
/// </summary>
[ApiController]
[Route("api/v1/trips/{tripId:guid}/messages")]
[Authorize]
public class MessagesController : ControllerBase
{
    private readonly IMessageService _messages;

    public MessagesController(IMessageService messages) => _messages = messages;

    [HttpPost]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Send(Guid tripId, [FromBody] SendMessageRequest request, CancellationToken ct)
    {
        var (userType, userId) = CurrentUser();
        if (userId is null) return Unauthorized();

        var response = await _messages.SendAsync(userType, userId.Value, tripId, request, ct);
        return StatusCode(StatusCodes.Status201Created, response);
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<MessageResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> List(Guid tripId, CancellationToken ct)
    {
        var (userType, userId) = CurrentUser();
        if (userId is null) return Unauthorized();

        return Ok(await _messages.ListForTripAsync(userType, userId.Value, tripId, ct));
    }

    private (string UserType, Guid? UserId) CurrentUser()
    {
        var userType = User.FindFirstValue("user_type") ?? string.Empty;
        var idStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return (userType, Guid.TryParse(idStr, out var id) ? id : null);
    }
}
