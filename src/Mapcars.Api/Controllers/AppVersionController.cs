using System.Security.Claims;
using Mapcars.Api.Filters;
using Mapcars.Application.Admins;
using Mapcars.Application.Settings.Dtos;
using Mapcars.Application.Settings.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mapcars.Api.Controllers;

/// <summary>
/// The newest published build of each mobile app, so the apps can offer a soft
/// "update available" prompt.
///
/// <para>
/// Reading is public: the apps ask at launch, and a signed-out user should still
/// be told. Nothing here is sensitive — it is the public store listing's
/// version number. Editing is an admin-portal action (Settings → App versions).
/// </para>
/// </summary>
[ApiController]
[Route("api/v1/app-version")]
public class AppVersionController(IAppVersionService versions) : ControllerBase
{
    /// <summary>Newest build for <c>app</c> (customer|driver) on <c>platform</c> (android|ios).</summary>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AppVersionEntryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AppVersionEntryDto>> Get(
        [FromQuery] string? app, [FromQuery] string? platform, CancellationToken ct)
    {
        var entry = await versions.GetAsync(app ?? "", platform ?? "", ct);
        return entry is null
            ? BadRequest(new { title = "app must be customer or driver, and platform android or ios." })
            : Ok(entry);
    }

    /// <summary>All four entries, for the admin portal.</summary>
    [HttpGet("admin")]
    [Authorize(Roles = "SuperAdmin,Admin")]
    [RequiresAdminMenu(AdminMenuPaths.AppVersions)]
    [ProducesResponseType(typeof(AppVersionsDocument), StatusCodes.Status200OK)]
    public async Task<ActionResult<AppVersionsDocument>> GetForAdmin(CancellationToken ct)
        => Ok(await versions.GetForAdminAsync(ct));

    /// <summary>Publish new version numbers. Live immediately.</summary>
    [HttpPut]
    [Authorize(Roles = "SuperAdmin,Admin")]
    [RequiresAdminMenu(AdminMenuPaths.AppVersions)]
    [ProducesResponseType(typeof(AppVersionsDocument), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AppVersionsDocument>> Update(
        [FromBody] AppVersionsDocument request, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var adminId))
            return Unauthorized();
        return Ok(await versions.UpdateAsync(request, adminId, ct));
    }
}
