using Mapcars.Application.Common.Interfaces;
using Mapcars.Application.Documents.Dtos;
using Mapcars.Application.Documents.Interfaces;
using Mapcars.Application.Documents.Mapping;
using Mapcars.Application.DriverReview.Dtos;
using Mapcars.Application.DriverReview.Interfaces;
using Mapcars.Application.Drivers.Interfaces;
using Mapcars.Application.Geo.Interfaces;
using Mapcars.Application.Notifications.Dtos;
using Mapcars.Application.Notifications.Interfaces;
using Mapcars.Application.Vehicles.Dtos;
using Mapcars.Application.Vehicles.Interfaces;
using Mapcars.Application.Vehicles.Mapping;
using Mapcars.Domain.Enums;
using Mapcars.Domain.Exceptions;
using Microsoft.Extensions.Logging;
using NotFoundException = Mapcars.Application.Common.Exceptions.NotFoundException;

namespace Mapcars.Application.DriverReview.Services;

/// <summary>
/// Admin document review and vehicle tier management. Reads a driver's KYC/vehicle documents,
/// records approve/reject decisions, manages vehicle tiers, and handles tier appeals.
/// </summary>
public class DriverReviewService : IDriverReviewService
{
    private static readonly HashSet<string> AllowedTiers = new(StringComparer.OrdinalIgnoreCase)
    {
        "economy", "comfort", "xl", "premium"
    };

    private readonly IDriverRepository _drivers;
    private readonly IDocumentRepository _documents;
    private readonly IVehicleRepository _vehicles;
    private readonly IVehicleTierAppealRepository _appeals;
    private readonly IFileStorageService _storage;
    private readonly IDriverLocationStore _locations;
    private readonly IEmailService _email;
    private readonly IPushService _push;
    private readonly IUnitOfWork _uow;
    private readonly ILogger<DriverReviewService> _logger;

    public DriverReviewService(
        IDriverRepository drivers,
        IDocumentRepository documents,
        IVehicleRepository vehicles,
        IVehicleTierAppealRepository appeals,
        IFileStorageService storage,
        IDriverLocationStore locations,
        IEmailService email,
        IPushService push,
        IUnitOfWork uow,
        ILogger<DriverReviewService> logger)
    {
        _drivers = drivers;
        _documents = documents;
        _vehicles = vehicles;
        _appeals = appeals;
        _storage = storage;
        _locations = locations;
        _email = email;
        _push = push;
        _uow = uow;
        _logger = logger;
    }

    public async Task<IReadOnlyList<DriverReviewListItem>> ListDriversAsync(DriverStatus? status, CancellationToken ct = default)
    {
        var drivers = await _drivers.ListByStatusAsync(status, ct);
        if (drivers.Count == 0) return [];

        // One query for every listed driver's documents, then group in memory.
        var docs = await _documents.ListForDriversAsync(drivers.Select(d => d.Id).ToArray(), ct);
        var byDriver = docs.Where(d => d.DriverId is not null)
            .GroupBy(d => d.DriverId!.Value)
            .ToDictionary(g => g.Key, g => g.ToList());

        return drivers.Select(d =>
        {
            byDriver.TryGetValue(d.Id, out var list);
            var total = list?.Count ?? 0;
            var pending = list?.Count(x => x.ReviewStatus == DocumentReviewStatus.Pending) ?? 0;
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var expired = list?.Count(x => x.ExpiresOn is not null && x.ExpiresOn < today) ?? 0;
            return new DriverReviewListItem(
                d.Id, d.FullName, d.Email, d.PhoneNumber, d.Status.ToString(),
                total, pending, expired, d.CreatedAtUtc);
        }).ToList();
    }

    public async Task<IReadOnlyList<DriverDocumentListItem>> ListAllDocumentsAsync(DocumentReviewStatus? status = null, CancellationToken ct = default)
    {
        var docs = await _documents.ListAllDriverDocumentsAsync(status, ct);
        return docs.Select(d => new DriverDocumentListItem(
            d.Id,
            d.DriverId ?? Guid.Empty,
            d.Driver?.FullName,
            d.Driver?.Email,
            d.Driver?.PhoneNumber,
            d.Driver?.Status.ToString() ?? "Unknown",
            d.Type.ToString(),
            d.StorageKey,
            d.OriginalFileName,
            d.ContentType,
            d.ReviewStatus.ToString(),
            d.ReviewedAtUtc,
            d.ExpiresOn,
            d.CreatedAtUtc
        )).ToList();
    }

    public async Task<DriverReviewDetail> GetDriverAsync(Guid driverId, CancellationToken ct = default)
    {
        var driver = await _drivers.GetByIdAsync(driverId, ct)
            ?? throw new NotFoundException("Driver", driverId);

        var vehicle = await _vehicles.GetByDriverAsync(driverId, ct);
        var documents = await _documents.ListForDriverAsync(driverId, ct);

        return new DriverReviewDetail(
            driver.Id,
            driver.FullName,
            driver.Email,
            driver.PhoneNumber,
            driver.DateOfBirth,
            driver.Address,
            driver.NationalIdNumber,
            driver.PhvLicenceNumber,
            driver.DrivingLicenceNumber,
            driver.PassportNumber,
            driver.EmergencyContactName,
            driver.EmergencyContactPhone,
            driver.MarketingConsent,
            driver.AverageRating,
            driver.RatingCount,
            driver.CancellationCount,
            driver.NoShowCount,
            driver.IsOnline,
            driver.Status.ToString(),
            driver.ProfilePictureKey is not null,
            vehicle?.ToResponse(),
            documents.Select(d => d.ToResponse()).ToList());
    }

    public async Task<FileContent?> GetDocumentContentAsync(Guid documentId, CancellationToken ct = default)
    {
        var document = await _documents.GetByIdAsync(documentId, ct);
        if (document is null) return null;

        var stream = await _storage.OpenReadAsync(document.StorageKey, ct);
        return stream is null ? null : new FileContent(stream, document.ContentType, document.OriginalFileName);
    }

    public async Task<DocumentResponse> ReviewDocumentAsync(Guid documentId, DocumentReviewStatus status, CancellationToken ct = default)
    {
        var document = await _documents.GetByIdAsync(documentId, ct)
            ?? throw new NotFoundException("Document", documentId);

        document.ReviewStatus = status;
        document.ReviewedAtUtc = DateTime.UtcNow;
        _documents.Update(document);
        await _uow.SaveChangesAsync(ct);

        return document.ToResponse();
    }

    public async Task<DocumentResponse> ReviewDocumentDeletionAsync(Guid documentId, string decision, Guid adminId, CancellationToken ct = default)
    {
        var document = await _documents.GetByIdAsync(documentId, ct)
            ?? throw new NotFoundException("Document", documentId);

        if (decision.Equals("Approved", StringComparison.OrdinalIgnoreCase))
        {
            _documents.Remove(document);
            await _uow.SaveChangesAsync(ct);
            return document.ToResponse();
        }
        else
        {
            document.IsDeletionRequested = false;
            document.DeletionReason = null;
            document.DeletionRequestedAtUtc = null;
            _documents.Update(document);
            await _uow.SaveChangesAsync(ct);
            return document.ToResponse();
        }
    }

    public async Task<IReadOnlyList<DriverDocumentListItem>> ListPendingDocumentDeletionsAsync(CancellationToken ct = default)
    {
        var docs = await _documents.ListPendingDeletionsAsync(ct);
        return docs.Select(d => new DriverDocumentListItem(
            d.Id,
            d.DriverId ?? Guid.Empty,
            d.Driver?.FullName,
            d.Driver?.Email,
            d.Driver?.PhoneNumber,
            d.Driver?.Status.ToString() ?? string.Empty,
            d.Type.ToString(),
            d.StorageKey,
            d.OriginalFileName,
            d.ContentType,
            d.ReviewStatus.ToString(),
            d.ReviewedAtUtc,
            d.ExpiresOn,
            d.CreatedAtUtc)).ToList();
    }

    public async Task<DriverReviewDetail> SetDriverStatusAsync(Guid driverId, DriverStatus status, CancellationToken ct = default)
    {
        var driver = await _drivers.GetByIdAsync(driverId, ct)
            ?? throw new NotFoundException("Driver", driverId);

        // A profile picture is how riders and other drivers recognise who's
        // arriving — no admin click can skip it, even by mistake.
        if (status == DriverStatus.Approved && driver.ProfilePictureKey is null)
            throw new DomainException("This driver must upload a profile picture before they can be approved.");

        driver.Status = status;

        // Anything other than Approved takes the driver off the road right now:
        // clear the online flag and drop them from the live GEO pool, so a
        // suspend/reject can't leave a working driver mid-shift.
        if (status != DriverStatus.Approved)
            driver.IsOnline = false;

        _drivers.Update(driver);
        await _uow.SaveChangesAsync(ct);

        if (status != DriverStatus.Approved)
            await _locations.RemoveAsync(driverId, ct);

        return await GetDriverAsync(driverId, ct);
    }

    // ── Vehicle Tier & Appeals ───────────────────────────────────────────────

    public async Task<VehicleResponse> SetVehicleTierAsync(Guid driverId, string tier, CancellationToken ct = default)
    {
        var vehicle = await _vehicles.GetByDriverAsync(driverId, ct)
            ?? throw new NotFoundException("Vehicle for driver", driverId);

        var normalisedTier = tier.Trim().ToLowerInvariant();
        if (!AllowedTiers.Contains(normalisedTier))
            throw new DomainException($"Invalid tier '{tier}'. Allowed tiers are: {string.Join(", ", AllowedTiers)}");

        vehicle.Tier = normalisedTier;
        _vehicles.Update(vehicle);
        await _uow.SaveChangesAsync(ct);

        return vehicle.ToResponse();
    }

    public async Task<IReadOnlyList<TierAppealListItem>> ListTierAppealsAsync(TierAppealStatus? status = null, CancellationToken ct = default)
    {
        var appeals = await _appeals.ListAllAsync(status, ct);
        return appeals.Select(a => a.ToListItem()).ToList();
    }

    public async Task<IReadOnlyList<VehicleTierAppealResponse>> GetTierAppealsForDriverAsync(Guid driverId, CancellationToken ct = default)
    {
        var appeals = await _appeals.ListForDriverAsync(driverId, ct);
        return appeals.Select(a => a.ToResponse(
            a.PhotoStorageKeys?.Select((_, idx) => $"/api/v1/admin/driver-review/tier-appeals/{a.Id}/photos/{idx}/content").ToList()
        )).ToList();
    }

    public async Task<FileContent?> GetAppealPhotoContentAsync(Guid appealId, int photoIndex, CancellationToken ct = default)
    {
        var appeal = await _appeals.GetByIdAsync(appealId, ct);
        if (appeal is null || appeal.PhotoStorageKeys is null || photoIndex < 0 || photoIndex >= appeal.PhotoStorageKeys.Count)
            return null;

        var key = appeal.PhotoStorageKeys[photoIndex];
        var stream = await _storage.OpenReadAsync(key, ct);
        if (stream is null) return null;

        return new FileContent(stream, "image/jpeg", Path.GetFileName(key));
    }

    public async Task<VehicleTierAppealResponse> ReviewTierAppealAsync(
        Guid appealId,
        Guid adminId,
        TierAppealStatus status,
        string? adminNotes = null,
        CancellationToken ct = default)
    {
        var appeal = await _appeals.GetByIdWithDetailsAsync(appealId, ct)
            ?? throw new NotFoundException("Tier appeal", appealId);

        if (appeal.Status != TierAppealStatus.Pending)
            throw new DomainException($"Appeal has already been decided ({appeal.Status}).");

        if (status is not (TierAppealStatus.Approved or TierAppealStatus.Rejected))
            throw new DomainException("Review decision must be 'Approved' or 'Rejected'.");

        appeal.Status = status;
        appeal.AdminNotes = adminNotes?.Trim();
        appeal.ReviewedByAdminId = adminId;
        appeal.ReviewedAtUtc = DateTime.UtcNow;
        _appeals.Update(appeal);

        // If approved, promote the vehicle's tier
        if (status == TierAppealStatus.Approved && appeal.Vehicle is not null)
        {
            appeal.Vehicle.Tier = appeal.RequestedTier;
            _vehicles.Update(appeal.Vehicle);
        }

        await _uow.SaveChangesAsync(ct);

        // Send Email and Push notifications to the driver (best-effort)
        await NotifyDriverAppealDecisionAsync(appeal, status, adminNotes, ct);

        return appeal.ToResponse(
            appeal.PhotoStorageKeys?.Select((_, idx) => $"/api/v1/admin/driver-review/tier-appeals/{appeal.Id}/photos/{idx}/content").ToList()
        );
    }

    private async Task NotifyDriverAppealDecisionAsync(
        Domain.Entities.VehicleTierAppeal appeal,
        TierAppealStatus status,
        string? adminNotes,
        CancellationToken ct)
    {
        var driver = appeal.Driver ?? await _drivers.GetByIdAsync(appeal.DriverId, ct);
        if (driver is null) return;

        var outcome = status == TierAppealStatus.Approved ? "Approved" : "Declined";
        var requestedTierDisplay = char.ToUpper(appeal.RequestedTier[0]) + appeal.RequestedTier[1..];

        // 1. Push Notification
        try
        {
            var pushTitle = status == TierAppealStatus.Approved
                ? $"Tier Appeal Approved! ({requestedTierDisplay})"
                : "Tier Appeal Update";

            var pushBody = status == TierAppealStatus.Approved
                ? $"Congratulations! Your vehicle has been updated to the {requestedTierDisplay} tier."
                : $"Your tier appeal for {requestedTierDisplay} was not approved. Check the app for details.";

            await _push.NotifyUserAsync(
                "driver",
                driver.Id,
                new PushMessage(
                    pushTitle,
                    pushBody,
                    new Dictionary<string, string>
                    {
                        ["type"] = "tier_appeal_decision",
                        ["appealId"] = appeal.Id.ToString(),
                        ["status"] = status.ToString(),
                    }),
                ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send push notification for tier appeal {AppealId}", appeal.Id);
        }

        // 2. Email Notification
        if (!string.IsNullOrWhiteSpace(driver.Email))
        {
            try
            {
                var subject = $"Mapcars — Vehicle Tier Appeal {outcome}";
                var greeting = !string.IsNullOrWhiteSpace(driver.FullName) ? $"Hi {driver.FullName}," : "Hello,";
                var notesText = !string.IsNullOrWhiteSpace(adminNotes)
                    ? $"\n\nAdmin note: {adminNotes}"
                    : string.Empty;

                var body = status == TierAppealStatus.Approved
                    ? $"{greeting}\n\nGreat news! Your appeal to upgrade your vehicle to the {requestedTierDisplay} tier has been approved by our operations team.\n\nYour vehicle is now eligible to receive {requestedTierDisplay} trip requests on the Mapcars platform.{notesText}\n\nSafe driving,\nThe Mapcars Team"
                    : $"{greeting}\n\nYour recent appeal to change your vehicle tier to {requestedTierDisplay} has been reviewed and was not approved at this time.{notesText}\n\nIf you have any questions or have further documentation, feel free to contact support or submit a new appeal with additional details.\n\nBest regards,\nThe Mapcars Team";

                await _email.SendAsync(
                    driver.Email,
                    subject,
                    body,
                    new EmailSendOptions(Category: "DriverTierAppeal", SentByAdminId: appeal.ReviewedByAdminId),
                    ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send email notification for tier appeal {AppealId}", appeal.Id);
            }
        }
    }
}
