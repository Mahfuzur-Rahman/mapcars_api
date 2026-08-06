using System.Security.Claims;
using Mapcars.Application.Drivers.Dtos;
using Mapcars.Application.Drivers.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Mapcars.Api.Controllers;

[ApiController]
[Route("api/v1/auth/drivers")]
public class DriverAuthController(IDriverAuthService authService) : ControllerBase
{
    [HttpPost("send-otp")]
    [EnableRateLimiting("otp")]
    public async Task<IActionResult> SendOtp([FromBody] DriverPhoneRequest req, CancellationToken ct)
        => Ok(await authService.SendPhoneOtpAsync(req.Phone, ct));

    [HttpPost("verify-phone")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> VerifyPhone([FromBody] DriverVerifyOtpRequest req, CancellationToken ct)
        => Ok(await authService.VerifyPhoneOtpAsync(req.Phone, req.Code, ct));

    [HttpPost("signup")]
    [EnableRateLimiting("otp")]
    public async Task<IActionResult> Signup([FromBody] DriverEmailSignUpRequest req, CancellationToken ct)
        => Ok(await authService.SignUpWithEmailAsync(req.Email, req.Password, req.FullName, ct));

    [HttpPost("resend-email")]
    [EnableRateLimiting("otp")]
    public async Task<IActionResult> ResendEmail([FromBody] DriverResendEmailRequest req, CancellationToken ct)
        => Ok(await authService.ResendEmailOtpAsync(req.Email, ct));

    [HttpPost("verify-email")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> VerifyEmail([FromBody] DriverVerifyEmailRequest req, CancellationToken ct)
        => Ok(await authService.VerifyEmailOtpAsync(req.Email, req.Code, ct));

    [HttpPost("login")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Login([FromBody] DriverEmailLoginRequest req, CancellationToken ct)
        => Ok(await authService.LoginWithEmailAsync(req.Email, req.Password, ct));

    [HttpPost("google")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Google([FromBody] DriverGoogleAuthRequest req, CancellationToken ct)
        => Ok(await authService.SignInWithGoogleAsync(req.IdToken, ct));

    /// <summary>Fetch the authenticated driver's full profile.</summary>
    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> GetProfile(CancellationToken ct)
    {
        if (!TryGetDriverId(out var driverId)) return Unauthorized();
        return Ok(await authService.GetProfileAsync(driverId, ct));
    }

    /// <summary>Update the authenticated driver's profile (name, DOB, address, national ID, optional email).</summary>
    [HttpPatch("me")]
    [Authorize]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateDriverProfileRequest req, CancellationToken ct)
    {
        if (!TryGetDriverId(out var driverId)) return Unauthorized();
        return Ok(await authService.UpdateProfileAsync(driverId, req, ct));
    }

    /// <summary>Toggle the authenticated driver's online/offline availability.</summary>
    [HttpPatch("me/availability")]
    [Authorize]
    public async Task<IActionResult> SetAvailability([FromBody] UpdateDriverAvailabilityRequest req, CancellationToken ct)
    {
        if (!TryGetDriverId(out var driverId)) return Unauthorized();
        return Ok(await authService.SetAvailabilityAsync(driverId, req.IsOnline, ct));
    }

    /// <summary>Upload/replace the authenticated driver's profile picture.</summary>
    [HttpPut("me/picture")]
    [Authorize]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(25 * 1024 * 1024)]
    public async Task<IActionResult> UploadPicture(IFormFile file, CancellationToken ct)
    {
        if (file.Length == 0)
            return BadRequest(new { title = "The uploaded file is empty." });
        if (!TryGetDriverId(out var driverId)) return Unauthorized();

        await using var stream = file.OpenReadStream();
        return Ok(await authService.UploadProfilePictureAsync(driverId, stream, file.FileName, file.ContentType, file.Length, ct));
    }

    /// <summary>Stream back the authenticated driver's profile picture.</summary>
    [HttpGet("me/picture")]
    [Authorize]
    public async Task<IActionResult> GetPicture(CancellationToken ct)
    {
        if (!TryGetDriverId(out var driverId)) return Unauthorized();
        var picture = await authService.GetProfilePictureAsync(driverId, ct);
        if (picture is null) return NotFound();

        // Stored content-type is client-supplied; stop the browser MIME-sniffing it.
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return File(picture.Value.Content, picture.Value.ContentType);
    }

    private bool TryGetDriverId(out Guid driverId)
        => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out driverId);
}
