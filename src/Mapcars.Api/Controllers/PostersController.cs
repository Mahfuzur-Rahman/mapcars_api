using System.Security.Claims;
using Mapcars.Application.Posters.Dtos;
using Mapcars.Application.Posters.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mapcars.Api.Controllers;

/// <summary>
/// Landing-page poster management. Admin CRUD is authenticated; the two
/// read routes used by the public landing page (<see cref="ListActive"/>,
/// <see cref="GetImage"/>) are anonymous — posters are public marketing
/// content, unlike the private KYC documents proxy.
/// </summary>
[ApiController]
[Route("api/v1/posters")]
[Authorize(Roles = "SuperAdmin,Admin")]
public class PostersController : ControllerBase
{
    private readonly IPosterService _posters;

    public PostersController(IPosterService posters) => _posters = posters;

    [HttpPost]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(25 * 1024 * 1024)]
    [ProducesResponseType(typeof(PosterResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PosterResponse>> Create(
        [FromForm] CreatePosterRequest request, [FromForm] IFormFile file, CancellationToken ct)
    {
        if (file.Length == 0)
            return BadRequest(new { title = "The uploaded file is empty." });

        await using var stream = file.OpenReadStream();
        var response = await _posters.CreateAsync(
            request, stream, file.FileName, file.ContentType, file.Length, CurrentAdminId(), ct);
        return StatusCode(StatusCodes.Status201Created, response);
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<PosterResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<PosterResponse>>> List(CancellationToken ct)
        => Ok(await _posters.ListAllAsync(ct));

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(PosterResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PosterResponse>> Update(Guid id, UpdatePosterRequest request, CancellationToken ct)
        => Ok(await _posters.UpdateAsync(id, request, ct));

    [HttpPost("{id:guid}/image")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(25 * 1024 * 1024)]
    [ProducesResponseType(typeof(PosterResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PosterResponse>> ReplaceImage(Guid id, [FromForm] IFormFile file, CancellationToken ct)
    {
        if (file.Length == 0)
            return BadRequest(new { title = "The uploaded file is empty." });

        await using var stream = file.OpenReadStream();
        var response = await _posters.ReplaceImageAsync(id, stream, file.FileName, file.ContentType, file.Length, ct);
        return Ok(response);
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _posters.DeleteAsync(id, ct);
        return NoContent();
    }

    [HttpGet("active")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(IReadOnlyList<PosterResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<PosterResponse>>> ListActive(CancellationToken ct)
        => Ok(await _posters.ListActiveAsync(ct));

    [HttpGet("{id:guid}/image")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetImage(Guid id, CancellationToken ct)
    {
        var result = await _posters.OpenImageAsync(id, ct);
        if (result is null) return NotFound();

        Response.Headers.CacheControl = "public, max-age=3600";
        return File(result.Value.Content, result.Value.ContentType);
    }

    private Guid? CurrentAdminId()
    {
        var idStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(idStr, out var id) ? id : null;
    }
}
