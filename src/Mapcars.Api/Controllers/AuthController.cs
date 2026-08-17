using Mapcars.Application.Auth.Dtos;
using Mapcars.Application.Auth.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Mapcars.Api.Controllers;

/// <summary>
/// The web app's single sign-in endpoint — detects Admin/Rider/Driver from the
/// credentials — plus the session endpoints (<c>refresh</c>/<c>logout</c>) that
/// every client shares, whichever door it signed in through.
/// </summary>
[ApiController]
[Route("api/v1/auth")]
public class AuthController(
    IUnifiedAuthService authService,
    IRefreshTokenService refreshTokens) : ControllerBase
{
    [HttpPost("login")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Login([FromBody] UnifiedLoginRequest request, CancellationToken ct)
        => Ok(await authService.LoginAsync(request, ct));

    [HttpPost("google")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Google([FromBody] UnifiedGoogleLoginRequest request, CancellationToken ct)
        => Ok(await authService.GoogleLoginAsync(request, ct));

    /// <summary>
    /// Exchanges a refresh token for a fresh access token, plus a rotated refresh
    /// token that replaces the one sent. This is what keeps a signed-in user
    /// signed in — without it the 60-minute access token expiry is a hard logout.
    /// <para>
    /// Deliberately <b>anonymous</b>: it is called precisely when the access
    /// token has expired, so requiring one would make it unreachable. The refresh
    /// token itself is the credential.
    /// </para>
    /// </summary>
    [HttpPost("refresh")]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(typeof(RefreshResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequest request, CancellationToken ct)
        => Ok(await refreshTokens.RefreshAsync(request.RefreshToken, ct));

    /// <summary>
    /// Signs out one device by revoking its refresh token server-side, so a copy
    /// taken off the device is dead too. Always 204 — signing out twice, or with
    /// a token the server has never seen, is not an error worth reporting.
    /// </summary>
    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout([FromBody] LogoutRequest request, CancellationToken ct)
    {
        await refreshTokens.RevokeAsync(request.RefreshToken, ct);
        return NoContent();
    }
}
