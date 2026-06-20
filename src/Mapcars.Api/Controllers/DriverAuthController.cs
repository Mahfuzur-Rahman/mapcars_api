using Mapcars.Application.Drivers.Dtos;
using Mapcars.Application.Drivers.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Mapcars.Api.Controllers;

[ApiController]
[Route("api/v1/auth/drivers")]
public class DriverAuthController(IDriverAuthService authService) : ControllerBase
{
    [HttpPost("send-otp")]
    public async Task<IActionResult> SendOtp([FromBody] DriverPhoneRequest req, CancellationToken ct)
        => Ok(await authService.SendPhoneOtpAsync(req.Phone, ct));

    [HttpPost("verify-phone")]
    public async Task<IActionResult> VerifyPhone([FromBody] DriverVerifyOtpRequest req, CancellationToken ct)
        => Ok(await authService.VerifyPhoneOtpAsync(req.Phone, req.Code, ct));

    [HttpPost("signup")]
    public async Task<IActionResult> Signup([FromBody] DriverEmailSignUpRequest req, CancellationToken ct)
        => Ok(await authService.SignUpWithEmailAsync(req.Email, req.Password, req.FullName, ct));

    [HttpPost("verify-email")]
    public async Task<IActionResult> VerifyEmail([FromBody] DriverVerifyEmailRequest req, CancellationToken ct)
        => Ok(await authService.VerifyEmailOtpAsync(req.Email, req.Code, ct));

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] DriverEmailLoginRequest req, CancellationToken ct)
        => Ok(await authService.LoginWithEmailAsync(req.Email, req.Password, ct));

    [HttpPost("google")]
    public async Task<IActionResult> Google([FromBody] DriverGoogleAuthRequest req, CancellationToken ct)
        => Ok(await authService.SignInWithGoogleAsync(req.IdToken, ct));
}
