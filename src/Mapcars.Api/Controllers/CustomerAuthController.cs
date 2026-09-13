using System.Security.Claims;
using Mapcars.Application.Common.Dtos;
using Mapcars.Application.Customers.Dtos;
using Mapcars.Application.Customers.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Mapcars.Api.Controllers;

[ApiController]
[Route("api/v1/auth/customers")]
// DEPRECATED alias — delete once web + both apps ship the renamed client.
[Route("api/v1/auth/riders")]
public class CustomerAuthController(ICustomerAuthService authService) : ControllerBase
{
    /// <summary>Step 1 of phone flow — sends a 6-digit SMS OTP.</summary>
    [HttpPost("send-otp")]
    [EnableRateLimiting("otp")]
    public async Task<IActionResult> SendOtp([FromBody] PhoneRequest req, CancellationToken ct)
        => Ok(await authService.SendPhoneOtpAsync(req.Phone, ct));

    /// <summary>Step 2 of phone flow — verifies OTP, creates or logs in the customer.</summary>
    [HttpPost("verify-phone")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> VerifyPhone([FromBody] VerifyOtpRequest req, CancellationToken ct)
        => Ok(await authService.VerifyPhoneOtpAsync(req.Phone, req.Code, ct));

    /// <summary>Email signup — creates a customer and sends an email OTP.</summary>
    [HttpPost("signup")]
    [EnableRateLimiting("otp")]
    public async Task<IActionResult> Signup([FromBody] EmailSignUpRequest req, CancellationToken ct)
        => Ok(await authService.SignUpWithEmailAsync(req.Email, req.Password, req.FullName, ct));

    /// <summary>Resends the email OTP (invalidates the previous code). For unverified accounts.</summary>
    [HttpPost("resend-email")]
    [EnableRateLimiting("otp")]
    public async Task<IActionResult> ResendEmail([FromBody] ResendEmailRequest req, CancellationToken ct)
        => Ok(await authService.ResendEmailOtpAsync(req.Email, ct));

    /// <summary>Verifies the email OTP sent during signup.</summary>
    [HttpPost("verify-email")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> VerifyEmail([FromBody] VerifyEmailRequest req, CancellationToken ct)
        => Ok(await authService.VerifyEmailOtpAsync(req.Email, req.Code, ct));

    /// <summary>Email login for existing verified customers.</summary>
    [HttpPost("login")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Login([FromBody] EmailLoginRequest req, CancellationToken ct)
        => Ok(await authService.LoginWithEmailAsync(req.Email, req.Password, ct));

    /// <summary>Google Sign-In — pass the ID token from the mobile/web Google SDK.</summary>
    [HttpPost("google")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Google([FromBody] GoogleAuthRequest req, CancellationToken ct)
        => Ok(await authService.SignInWithGoogleAsync(req.IdToken, req.SignUp, ct));

    /// <summary>Get the authenticated customer's full profile.</summary>
    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> GetProfile(CancellationToken ct)
    {
        var customerIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(customerIdStr, out var customerId))
            return Unauthorized();
        return Ok(await authService.GetProfileAsync(customerId, ct));
    }

    /// <summary>Update the authenticated customer's profile (name, email, emergency contact, marketing consent, accessibility needs).</summary>
    [HttpPatch("me")]
    [Authorize]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest req, CancellationToken ct)
    {
        var customerIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(customerIdStr, out var customerId))
            return Unauthorized();
        return Ok(await authService.UpdateProfileAsync(customerId, req, ct));
    }

    /// <summary>Changes the authenticated customer's own password.</summary>
    [HttpPost("me/change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest req, CancellationToken ct)
    {
        var customerIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(customerIdStr, out var customerId))
            return Unauthorized();
        await authService.ChangePasswordAsync(customerId, req, ct);
        return NoContent();
    }

    /// <summary>Upload/replace the authenticated customer's profile picture.</summary>
    [HttpPut("me/picture")]
    [Authorize]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(25 * 1024 * 1024)]
    public async Task<IActionResult> UploadPicture(IFormFile file, CancellationToken ct)
    {
        if (file.Length == 0)
            return BadRequest(new { title = "The uploaded file is empty." });
        var customerIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(customerIdStr, out var customerId))
            return Unauthorized();

        await using var stream = file.OpenReadStream();
        return Ok(await authService.UploadProfilePictureAsync(customerId, stream, file.FileName, file.ContentType, file.Length, ct));
    }

    /// <summary>Stream back the authenticated customer's profile picture.</summary>
    [HttpGet("me/picture")]
    [Authorize]
    public async Task<IActionResult> GetPicture(CancellationToken ct)
    {
        var customerIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(customerIdStr, out var customerId))
            return Unauthorized();
        var picture = await authService.GetProfilePictureAsync(customerId, ct);
        if (picture is null) return NotFound();

        // Stored content-type is client-supplied; stop the browser MIME-sniffing it.
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return File(picture.Value.Content, picture.Value.ContentType);
    }
}
