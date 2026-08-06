using Mapcars.Application.DriverReview.Dtos;
using Mapcars.Application.DriverReview.Interfaces;
using Mapcars.Domain.Enums;
using Mapcars.Domain.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mapcars.Api.Controllers;

/// <summary>
/// Admin driver-verification workflow: browse drivers awaiting approval, view
/// their uploaded documents, approve/reject each document, and set the driver's
/// overall status. Admin-only — both SuperAdmin and Admin roles.
/// </summary>
[ApiController]
[Route("api/v1/admin/driver-review")]
[Authorize(Roles = "SuperAdmin,Admin")]
public class AdminDriverReviewController : ControllerBase
{
    private readonly IDriverReviewService _review;

    public AdminDriverReviewController(IDriverReviewService review) => _review = review;

    /// <summary>List drivers for review, optionally filtered by status (e.g. ?status=PendingApproval).</summary>
    [HttpGet("drivers")]
    [ProducesResponseType(typeof(IReadOnlyList<DriverReviewListItem>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListDrivers([FromQuery] DriverStatus? status, CancellationToken ct)
        => Ok(await _review.ListDriversAsync(status, ct));

    /// <summary>Full detail for one driver: profile, vehicle, and documents.</summary>
    [HttpGet("drivers/{driverId:guid}")]
    [ProducesResponseType(typeof(DriverReviewDetail), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDriver(Guid driverId, CancellationToken ct)
        => Ok(await _review.GetDriverAsync(driverId, ct));

    /// <summary>Stream a document's bytes for viewing (image or PDF). Never a public URL.</summary>
    [HttpGet("documents/{documentId:guid}/content")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDocumentContent(Guid documentId, CancellationToken ct)
    {
        var file = await _review.GetDocumentContentAsync(documentId, ct);
        if (file is null) return NotFound();

        // Content-type is client-supplied at upload; block MIME sniffing.
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return File(file.Content, file.ContentType);
    }

    /// <summary>Approve or reject a single document.</summary>
    [HttpPut("documents/{documentId:guid}/review")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ReviewDocument(Guid documentId, [FromBody] ReviewDocumentRequest request, CancellationToken ct)
    {
        var status = ParseReviewStatus(request.Status);
        return Ok(await _review.ReviewDocumentAsync(documentId, status, ct));
    }

    /// <summary>Set the driver's overall status (Approved / Rejected / Suspended / PendingApproval).</summary>
    [HttpPut("drivers/{driverId:guid}/status")]
    [ProducesResponseType(typeof(DriverReviewDetail), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetDriverStatus(Guid driverId, [FromBody] SetDriverStatusRequest request, CancellationToken ct)
    {
        var status = ParseDriverStatus(request.Status);
        return Ok(await _review.SetDriverStatusAsync(driverId, status, ct));
    }

    // JSON enum binding isn't configured globally, so parse names explicitly and
    // surface a clean 400 (via DomainException) on anything unexpected — and
    // restrict document review to the two valid outcomes (never back to Pending).
    private static DocumentReviewStatus ParseReviewStatus(string value)
    {
        if (Enum.TryParse<DocumentReviewStatus>(value, ignoreCase: true, out var status)
            && status is DocumentReviewStatus.Approved or DocumentReviewStatus.Rejected)
            return status;
        throw new DomainException("Status must be 'Approved' or 'Rejected'.");
    }

    private static DriverStatus ParseDriverStatus(string value)
    {
        if (Enum.TryParse<DriverStatus>(value, ignoreCase: true, out var status))
            return status;
        throw new DomainException("Status must be one of: PendingApproval, Approved, Suspended, Rejected.");
    }
}
