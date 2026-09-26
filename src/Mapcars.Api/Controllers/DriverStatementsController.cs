using System.Security.Claims;
using Mapcars.Application.Statements.Dtos;
using Mapcars.Application.Statements.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mapcars.Api.Controllers;

/// <summary>A driver's own weekly statements (money in pence).</summary>
[ApiController]
[Route("api/v1/drivers/me/statements")]
[Authorize(Roles = "driver")]
public class DriverStatementsController(IDriverStatementService statements) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<DriverStatementSummary>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(CancellationToken ct)
        => DriverId() is Guid id ? Ok(await statements.ListMineAsync(id, ct)) : Unauthorized();

    [HttpGet("{statementId:guid}")]
    [ProducesResponseType(typeof(DriverStatementDetail), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid statementId, CancellationToken ct)
        => DriverId() is Guid id ? Ok(await statements.GetMineAsync(id, statementId, ct)) : Unauthorized();

    private Guid? DriverId()
        => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
}

/// <summary>Every driver's statements, for the admin portal.</summary>
[ApiController]
[Route("api/v1/admin/statements")]
[Authorize(Roles = "SuperAdmin,Admin")]
public class AdminStatementsController(IDriverStatementService statements) : ControllerBase
{
    /// <summary>Newest week first. <paramref name="periodStart"/> (UTC instant of the week's start) narrows to one week.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<DriverStatementSummary>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] DateTime? periodStart,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 50,
        CancellationToken ct = default)
        => Ok(await statements.AdminListAsync(periodStart?.ToUniversalTime(), skip, take, ct));

    [HttpGet("{statementId:guid}")]
    [ProducesResponseType(typeof(DriverStatementDetail), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid statementId, CancellationToken ct)
        => Ok(await statements.AdminGetAsync(statementId, ct));
}
