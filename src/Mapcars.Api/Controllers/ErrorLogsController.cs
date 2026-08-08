using System.Security.Claims;
using Mapcars.Application.ErrorLogs.Dtos;
using Mapcars.Application.ErrorLogs.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Mapcars.Api.Controllers;

/// <summary>
/// Client-side crash reporting. Anonymous on purpose — the web app and both
/// mobile apps must be able to report a failure that happened *before* sign-in
/// (or that broke sign-in itself), which is exactly when a report is most
/// useful. Abuse is bounded by the "errors" rate-limit policy plus the
/// length caps in ErrorLogService.
///
/// Reading the log is a different matter entirely — see
/// <see cref="AdminErrorLogsController"/>.
/// </summary>
[ApiController]
[Route("api/v1/error-logs")]
public class ErrorLogsController : ControllerBase
{
    private readonly IErrorLogService _errors;

    public ErrorLogsController(IErrorLogService errors) => _errors = errors;

    [HttpPost]
    [AllowAnonymous]
    [EnableRateLimiting("errors")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    public async Task<IActionResult> Report([FromBody] ReportErrorRequest request, CancellationToken ct)
    {
        await _errors.ReportAsync(request, BuildContext(), ct);

        // Accepted, not Created: the client gets no id back and must never wait
        // on — or retry — its own crash report.
        return Accepted();
    }

    /// <summary>
    /// Identity and IP as the server sees them. A client could put anything in
    /// the payload, so these are read from the validated token and the
    /// connection instead.
    /// </summary>
    private ErrorRequestContext BuildContext()
    {
        var userType = User.FindFirstValue("userType")
            ?? User.FindFirstValue(ClaimTypes.Role);
        Guid? userId = Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

        return new ErrorRequestContext(
            UserType: User.Identity?.IsAuthenticated == true ? userType : null,
            UserId: userId,
            UserAgent: Request.Headers.UserAgent.ToString(),
            IpAddress: ClientIp());
    }

    /// <summary>Real client IP — we sit behind Nginx, so X-Forwarded-For wins.</summary>
    private string? ClientIp()
    {
        var forwarded = Request.Headers["X-Forwarded-For"].ToString();
        if (!string.IsNullOrWhiteSpace(forwarded))
            return forwarded.Split(',')[0].Trim();

        return HttpContext.Connection.RemoteIpAddress?.ToString();
    }
}

/// <summary>
/// The Error Logger page in the admin portal. SuperAdmin and Admin only —
/// stack traces and user ids are not something a rider or driver token should
/// ever reach.
/// </summary>
[ApiController]
[Route("api/v1/admin/error-logs")]
[Authorize(Roles = "SuperAdmin,Admin")]
public class AdminErrorLogsController : ControllerBase
{
    private readonly IErrorLogService _errors;

    public AdminErrorLogsController(IErrorLogService errors) => _errors = errors;

    /// <summary>Newest first, filterable by source/level/resolved plus a text search.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ErrorLogPage), StatusCodes.Status200OK)]
    public async Task<ActionResult<ErrorLogPage>> List(
        [FromQuery] string? source,
        [FromQuery] string? level,
        [FromQuery] bool? resolved,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
        => Ok(await _errors.ListAsync(source, level, resolved, search, page, pageSize, ct));

    /// <summary>Counts for the header strip.</summary>
    [HttpGet("summary")]
    [ProducesResponseType(typeof(ErrorLogSummary), StatusCodes.Status200OK)]
    public async Task<ActionResult<ErrorLogSummary>> Summary(CancellationToken ct)
        => Ok(await _errors.SummaryAsync(ct));

    /// <summary>The full entry, stack trace included.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ErrorLogDetail), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ErrorLogDetail>> Get(Guid id, CancellationToken ct)
        => Ok(await _errors.GetAsync(id, ct));

    /// <summary>Mark an entry handled (or put it back).</summary>
    [HttpPatch("{id:guid}/resolved")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetResolved(Guid id, [FromBody] SetResolvedRequest body, CancellationToken ct)
    {
        Guid? adminId = Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var parsed)
            ? parsed
            : null;

        return await _errors.SetResolvedAsync(id, body.Resolved, adminId, ct)
            ? NoContent()
            : NotFound();
    }

    /// <summary>
    /// Housekeeping — drop everything older than N days. SuperAdmin only: it's
    /// the one destructive action on this table.
    /// </summary>
    [HttpDelete("purge")]
    [Authorize(Roles = "SuperAdmin")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Purge([FromQuery] int olderThanDays = 30, CancellationToken ct = default)
        => Ok(new { deleted = await _errors.PurgeAsync(olderThanDays, ct) });
}

public record SetResolvedRequest(bool Resolved);
