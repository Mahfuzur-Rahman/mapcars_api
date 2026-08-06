using Mapcars.Application.Auth.Dtos;
using Mapcars.Application.Auth.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Mapcars.Api.Controllers;

/// <summary>The web app's single sign-in endpoint — detects Admin/Rider/Driver from the credentials.</summary>
[ApiController]
[Route("api/v1/auth")]
public class AuthController(IUnifiedAuthService authService) : ControllerBase
{
    [HttpPost("login")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Login([FromBody] UnifiedLoginRequest request, CancellationToken ct)
        => Ok(await authService.LoginAsync(request, ct));
}
