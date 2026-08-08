using System.Security.Claims;
using Mapcars.Application.Admins.Dtos;
using Mapcars.Application.Admins.Interfaces;
using Mapcars.Application.Common.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Mapcars.Api.Controllers;

[ApiController]
[Route("api/v1/admin/auth")]
public class AdminAuthController(IAdminAuthService authService) : ControllerBase
{
    /// <summary>
    /// One-time setup — creates the SuperAdmin account.
    /// Fails if any admin already exists (prevents takeover).
    /// </summary>
    [HttpPost("setup")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Setup([FromBody] CreateAdminRequest request, CancellationToken ct)
    {
        var result = await authService.SetupSuperAdminAsync(request, ct);
        return Ok(result);
    }

    [HttpPost("login")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        var result = await authService.LoginAsync(request, ct);
        return Ok(result);
    }

    /// <summary>Returns the current admin's profile + menu tree (refreshes token).</summary>
    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> Me(CancellationToken ct)
    {
        var adminId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var result = await authService.GetCurrentAdminAsync(adminId, ct);
        return Ok(result);
    }

    /// <summary>Changes the authenticated admin's own password.</summary>
    [Authorize]
    [HttpPost("me/change-password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request, CancellationToken ct)
    {
        var adminId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        await authService.ChangePasswordAsync(adminId, request, ct);
        return NoContent();
    }

    /// <summary>SuperAdmin only — creates a new admin account.</summary>
    [Authorize(Roles = "SuperAdmin")]
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] CreateAdminRequest request, CancellationToken ct)
    {
        var createdBy = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var result = await authService.RegisterAsync(request, createdBy, ct);
        return StatusCode(201, result);
    }
}
