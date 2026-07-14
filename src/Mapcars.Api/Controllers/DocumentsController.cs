using System.Security.Claims;
using Mapcars.Application.Documents.Dtos;
using Mapcars.Application.Documents.Interfaces;
using Mapcars.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mapcars.Api.Controllers;

/// <summary>
/// Document upload/listing for the authenticated rider OR driver. Which
/// DocumentType values are accepted depends on the caller's role — enforced
/// in IDocumentService, not here (this is a thin HTTP translation layer).
/// </summary>
[ApiController]
[Route("api/v1/documents")]
[Authorize]
public class DocumentsController : ControllerBase
{
    private readonly IDocumentService _documents;

    public DocumentsController(IDocumentService documents) => _documents = documents;

    [HttpPost]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(DocumentResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<DocumentResponse>> Upload(
        [FromForm] DocumentType type, [FromForm] IFormFile file, CancellationToken ct)
    {
        if (file.Length == 0)
            return BadRequest(new { title = "The uploaded file is empty." });

        var (userType, userId) = CurrentUser();
        if (userId is null)
            return Unauthorized();

        await using var stream = file.OpenReadStream();
        var response = await _documents.UploadAsync(
            userType, userId.Value, type, stream, file.FileName, file.ContentType, ct);

        return StatusCode(StatusCodes.Status201Created, response);
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<DocumentResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<DocumentResponse>>> List(CancellationToken ct)
    {
        var (userType, userId) = CurrentUser();
        if (userId is null)
            return Unauthorized();

        return Ok(await _documents.ListAsync(userType, userId.Value, ct));
    }

    private (string UserType, Guid? UserId) CurrentUser()
    {
        var userType = User.FindFirstValue("user_type") ?? string.Empty;
        var idStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return (userType, Guid.TryParse(idStr, out var id) ? id : null);
    }
}
