using System.Security.Claims;
using Mapcars.Application.Notifications.Dtos;
using Mapcars.Application.Notifications.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mapcars.Api.Controllers;

/// <summary>
/// Push-notification device registration. A signed-in rider or driver registers
/// its FCM token here after login (and on token refresh), and unregisters on
/// logout. The owner (userType + id) comes from the JWT, never the body.
/// </summary>
[ApiController]
[Route("api/v1/devices")]
[Authorize(Roles = "rider,driver")]
public class DevicesController(IPushService push) : ControllerBase
{
    [HttpPost("register")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Register([FromBody] RegisterDeviceRequest req, CancellationToken ct)
    {
        if (!TryGetCaller(out var userType, out var userId)) return Unauthorized();
        await push.RegisterAsync(userType, userId, req, ct);
        return NoContent();
    }

    /// <summary>Unregister a token (e.g. on logout). Idempotent.</summary>
    [HttpDelete("{token}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Unregister(string token, CancellationToken ct)
    {
        await push.UnregisterAsync(token, ct);
        return NoContent();
    }

    private bool TryGetCaller(out string userType, out Guid userId)
    {
        userType = User.IsInRole("driver") ? "driver" : "rider";
        return Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);
    }
}
