using System.Security.Claims;
using Mapcars.Application.DriverReview.Dtos;
using Mapcars.Application.DriverReview.Interfaces;
using Mapcars.Application.Vehicles.Dtos;
using Mapcars.Domain.Enums;
using Mapcars.Domain.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mapcars.Api.Controllers;

/// <summary>
/// Admin driver-verification and vehicle tier workflow: browse drivers awaiting approval, view
/// their uploaded documents, approve/reject each document, set driver status, manage vehicle tiers,
/// and review tier upgrade appeals. Admin-only — both SuperAdmin and Admin roles.
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

    /// <summary>List all driver documents across all drivers, optionally filtered by review status.</summary>
    [HttpGet("documents")]
    [ProducesResponseType(typeof(IReadOnlyList<DriverDocumentListItem>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListDocuments([FromQuery] DocumentReviewStatus? status, CancellationToken ct)
        => Ok(await _review.ListAllDocumentsAsync(status, ct));

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

    // ── Vehicle Tier & Appeal Management ─────────────────────────────────────

    /// <summary>Admin directly sets/overrides a driver's vehicle tier.</summary>
    [HttpPut("drivers/{driverId:guid}/tier")]
    [ProducesResponseType(typeof(VehicleResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetVehicleTier(Guid driverId, [FromBody] SetVehicleTierRequest request, CancellationToken ct)
        => Ok(await _review.SetVehicleTierAsync(driverId, request.Tier, ct));

    /// <summary>List tier appeals for a specific driver.</summary>
    [HttpGet("drivers/{driverId:guid}/appeals")]
    [ProducesResponseType(typeof(IReadOnlyList<VehicleTierAppealResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDriverAppeals(Guid driverId, CancellationToken ct)
        => Ok(await _review.GetTierAppealsForDriverAsync(driverId, ct));

    /// <summary>List tier appeals across all drivers with optional status filter.</summary>
    [HttpGet("tier-appeals")]
    [ProducesResponseType(typeof(IReadOnlyList<TierAppealListItem>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListTierAppeals([FromQuery] TierAppealStatus? status, CancellationToken ct)
        => Ok(await _review.ListTierAppealsAsync(status, ct));

    /// <summary>Stream an attached photo from a tier appeal for admin preview.</summary>
    [HttpGet("tier-appeals/{appealId:guid}/photos/{photoIndex:int}/content")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAppealPhotoContent(Guid appealId, int photoIndex, CancellationToken ct)
    {
        var file = await _review.GetAppealPhotoContentAsync(appealId, photoIndex, ct);
        if (file is null) return NotFound();

        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return File(file.Content, file.ContentType);
    }

    /// <summary>Approve or reject a tier appeal (triggers automatic vehicle tier upgrade on approve, plus email and push).</summary>
    [HttpPut("tier-appeals/{appealId:guid}/review")]
    [ProducesResponseType(typeof(VehicleTierAppealResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ReviewTierAppeal(Guid appealId, [FromBody] ReviewTierAppealRequest request, CancellationToken ct)
    {
        if (!TryGetAdminId(out var adminId)) return Unauthorized();

        var status = ParseAppealStatus(request.Status);
        return Ok(await _review.ReviewTierAppealAsync(appealId, adminId, status, request.AdminNotes, ct));
    }

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

    private static TierAppealStatus ParseAppealStatus(string value)
    {
        if (Enum.TryParse<TierAppealStatus>(value, ignoreCase: true, out var status)
            && status is TierAppealStatus.Approved or TierAppealStatus.Rejected)
            return status;
        throw new DomainException("Appeal status must be 'Approved' or 'Rejected'.");
    }

    private bool TryGetAdminId(out Guid adminId)
        => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out adminId);
}
