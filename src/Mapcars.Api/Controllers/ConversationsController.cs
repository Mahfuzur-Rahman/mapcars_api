using System.Security.Claims;
using Mapcars.Application.Messages.Dtos;
using Mapcars.Application.Messages.Interfaces;
using Mapcars.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mapcars.Api.Controllers;

/// <summary>
/// The caller's Messages inbox: one conversation per trip, unread counts, and
/// mark-as-read. Sending stays on <see cref="MessagesController"/>.
/// </summary>
[ApiController]
[Route("api/v1/conversations")]
[Authorize(Roles = UserTypes.CustomerOrDriverRoles)]
public class ConversationsController : ControllerBase
{
    private readonly IConversationService _conversations;

    public ConversationsController(IConversationService conversations) => _conversations = conversations;

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ConversationResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var (userType, userId) = CurrentUser();
        if (userId is null) return Unauthorized();
        return Ok(await _conversations.ListAsync(userType, userId.Value, ct));
    }

    [HttpGet("unread")]
    [ProducesResponseType(typeof(UnreadSummaryResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Unread(CancellationToken ct)
    {
        var (userType, userId) = CurrentUser();
        if (userId is null) return Unauthorized();
        return Ok(await _conversations.UnreadAsync(userType, userId.Value, ct));
    }

    [HttpGet("{tripId:guid}")]
    [ProducesResponseType(typeof(ConversationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid tripId, CancellationToken ct)
    {
        var (userType, userId) = CurrentUser();
        if (userId is null) return Unauthorized();
        return Ok(await _conversations.GetAsync(userType, userId.Value, tripId, ct));
    }

    [HttpPost("{tripId:guid}/read")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MarkRead(Guid tripId, CancellationToken ct)
    {
        var (userType, userId) = CurrentUser();
        if (userId is null) return Unauthorized();
        await _conversations.MarkReadAsync(userType, userId.Value, tripId, ct);
        return NoContent();
    }

    private (string UserType, Guid? UserId) CurrentUser()
    {
        var userType = UserTypes.Canonical(User.FindFirstValue("user_type"));
        var idStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return (userType, Guid.TryParse(idStr, out var id) ? id : null);
    }
}
