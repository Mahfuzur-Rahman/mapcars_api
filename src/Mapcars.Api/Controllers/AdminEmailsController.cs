using System.Security.Claims;
using Mapcars.Application.Emails.Dtos;
using Mapcars.Application.Emails.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mapcars.Api.Controllers;

/// <summary>
/// The Email page in the admin portal — every send the platform has made
/// (OTP codes, welcome emails, ...) plus the Compose feature for ad-hoc mail.
/// SuperAdmin and Admin only, same as <see cref="AdminErrorLogsController"/>.
/// </summary>
[ApiController]
[Route("api/v1/admin/emails")]
[Authorize(Roles = "SuperAdmin,Admin")]
public class AdminEmailsController : ControllerBase
{
    private readonly IEmailAdminService _emails;

    public AdminEmailsController(IEmailAdminService emails) => _emails = emails;

    /// <summary>Newest first, filterable by category/status plus a text search.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(EmailLogPage), StatusCodes.Status200OK)]
    public async Task<ActionResult<EmailLogPage>> List(
        [FromQuery] string? category,
        [FromQuery] string? status,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
        => Ok(await _emails.ListAsync(category, status, search, page, pageSize, ct));

    /// <summary>The full entry, body included.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(EmailLogDetail), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmailLogDetail>> Get(Guid id, CancellationToken ct)
        => Ok(await _emails.GetAsync(id, ct));

    /// <summary>Sends an ad-hoc email from one of the @mapcars.uk addresses.</summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    public async Task<IActionResult> Compose([FromBody] ComposeEmailRequest request, CancellationToken ct)
    {
        var adminId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        await _emails.ComposeAsync(request, adminId, ct);
        return Accepted();
    }
}
