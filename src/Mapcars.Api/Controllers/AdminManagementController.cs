using Mapcars.Api.Filters;
using Mapcars.Application.Admins;
using Mapcars.Application.Admins.Dtos;
using Mapcars.Application.Admins.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mapcars.Api.Controllers;

/// <summary>
/// SuperAdmin-only management of admins and their menu access.
/// (Login / setup / self-registration live in <see cref="AdminAuthController"/>.)
/// <para>
/// The role stays SuperAdmin even though the Admin Users menu could be granted
/// to an Admin by override: with this surface an Admin could grant themselves
/// every menu. The menu gate adds only the active-account check.
/// </para>
/// </summary>
[ApiController]
[Route("api/v1/admin")]
[Authorize(Roles = "SuperAdmin")]
[RequiresAdminMenu(AdminMenuPaths.AdminUsers)]
public class AdminManagementController(IAdminManagementService management) : ControllerBase
{
    /// <summary>List every admin, with their effective menu count.</summary>
    [HttpGet("admins")]
    [ProducesResponseType(typeof(List<AdminListItemResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAdmins(CancellationToken ct)
        => Ok(await management.GetAllAdminsAsync(ct));

    /// <summary>The full menu catalog (everything the platform offers) as a tree.</summary>
    [HttpGet("menus")]
    [ProducesResponseType(typeof(List<MenuResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMenuCatalog(CancellationToken ct)
        => Ok(await management.GetMenuCatalogAsync(ct));

    /// <summary>Get one admin's menu access (whole catalog annotated allowed / role-default).</summary>
    [HttpGet("admins/{id:guid}/menus")]
    [ProducesResponseType(typeof(AdminMenuAccessResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAdminMenus(Guid id, CancellationToken ct)
        => Ok(await management.GetAdminMenuAccessAsync(id, ct));

    /// <summary>Set the complete set of menus an admin can see.</summary>
    [HttpPut("admins/{id:guid}/menus")]
    [ProducesResponseType(typeof(AdminMenuAccessResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetAdminMenus(Guid id, [FromBody] UpdateAdminMenusRequest request, CancellationToken ct)
        => Ok(await management.SetAdminMenuAccessAsync(id, request.MenuIds, ct));
}
