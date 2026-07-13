using System.Security.Claims;
using Mapcars.Application.Riders.Dtos;
using Mapcars.Application.Riders.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mapcars.Api.Controllers;

[ApiController]
[Route("api/v1/auth/riders")]
public class RiderAuthController(IRiderAuthService authService) : ControllerBase
{
    /// <summary>Step 1 of phone flow — sends a 6-digit SMS OTP.</summary>
    [HttpPost("send-otp")]
    public async Task<IActionResult> SendOtp([FromBody] PhoneRequest req, CancellationToken ct)
        => Ok(await authService.SendPhoneOtpAsync(req.Phone, ct));

    /// <summary>Step 2 of phone flow — verifies OTP, creates or logs in the rider.</summary>
    [HttpPost("verify-phone")]
    public async Task<IActionResult> VerifyPhone([FromBody] VerifyOtpRequest req, CancellationToken ct)
        => Ok(await authService.VerifyPhoneOtpAsync(req.Phone, req.Code, ct));

    /// <summary>Email signup — creates a rider and sends an email OTP.</summary>
    [HttpPost("signup")]
    public async Task<IActionResult> Signup([FromBody] EmailSignUpRequest req, CancellationToken ct)
        => Ok(await authService.SignUpWithEmailAsync(req.Email, req.Password, req.FullName, ct));

    /// <summary>Resends the email OTP (invalidates the previous code). For unverified accounts.</summary>
    [HttpPost("resend-email")]
    public async Task<IActionResult> ResendEmail([FromBody] ResendEmailRequest req, CancellationToken ct)
        => Ok(await authService.ResendEmailOtpAsync(req.Email, ct));

    /// <summary>Verifies the email OTP sent during signup.</summary>
    [HttpPost("verify-email")]
    public async Task<IActionResult> VerifyEmail([FromBody] VerifyEmailRequest req, CancellationToken ct)
        => Ok(await authService.VerifyEmailOtpAsync(req.Email, req.Code, ct));

    /// <summary>Email login for existing verified riders.</summary>
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] EmailLoginRequest req, CancellationToken ct)
        => Ok(await authService.LoginWithEmailAsync(req.Email, req.Password, ct));

    /// <summary>Google Sign-In — pass the ID token from the mobile/web Google SDK.</summary>
    [HttpPost("google")]
    public async Task<IActionResult> Google([FromBody] GoogleAuthRequest req, CancellationToken ct)
        => Ok(await authService.SignInWithGoogleAsync(req.IdToken, ct));

    /// <summary>Update the authenticated rider's profile (full name, optional email).</summary>
    [HttpPatch("me")]
    [Authorize]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest req, CancellationToken ct)
    {
        var riderIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(riderIdStr, out var riderId))
            return Unauthorized();
        return Ok(await authService.UpdateProfileAsync(riderId, req.FullName, req.Email, ct));
    }
}
