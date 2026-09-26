using Mapcars.Application.Auth.Dtos;
using Mapcars.Application.Auth.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Mapcars.Api.Controllers;

/// <summary>
/// The web app's single sign-in endpoint — detects Admin/Customer/Driver from the
/// credentials — plus the session endpoints (<c>refresh</c>/<c>logout</c>) that
/// every client shares, whichever door it signed in through.
/// </summary>
[ApiController]
[Route("api/v1/auth")]
public class AuthController(
    IUnifiedAuthService authService,
    IRefreshTokenService refreshTokens,
    IPasswordResetService passwordReset) : ControllerBase
{
    /// <summary>
    /// Emails a 6-digit reset code to a customer or driver account. Always 202,
    /// whether or not the account exists — this must not answer "is this email
    /// registered?". Rate-limited as a billable send.
    /// </summary>
    [HttpPost("password/forgot")]
    [EnableRateLimiting("otp")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request, CancellationToken ct)
    {
        await passwordReset.RequestAsync(request, ct);
        return Accepted();
    }

    /// <summary>
    /// Sets a new password with the emailed code, and signs the account out on
    /// every device. 400 on a wrong, expired or burnt code (5 wrong guesses burn it).
    /// </summary>
    [HttpPost("password/reset")]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request, CancellationToken ct)
    {
        await passwordReset.ResetAsync(request, ct);
        return NoContent();
    }

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
