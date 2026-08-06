using Mapcars.Application.Common.Interfaces;
using Mapcars.Application.Documents.Dtos;
using Mapcars.Application.Documents.Interfaces;
using Mapcars.Application.Documents.Mapping;
using Mapcars.Application.DriverReview.Dtos;
using Mapcars.Application.DriverReview.Interfaces;
using Mapcars.Application.Drivers.Interfaces;
using Mapcars.Application.Vehicles.Interfaces;
using Mapcars.Application.Vehicles.Mapping;
using Mapcars.Domain.Enums;
using NotFoundException = Mapcars.Application.Common.Exceptions.NotFoundException;

namespace Mapcars.Application.DriverReview.Services;

/// <summary>
/// Admin document review. Reads a driver's KYC/vehicle documents, streams them
/// for viewing, and records approve/reject decisions. The overall driver
/// go/no-go (DriverStatus) is a separate explicit admin action so an admin can
/// review each document before letting a driver work.
/// </summary>
public class DriverReviewService : IDriverReviewService
{
    private readonly IDriverRepository _drivers;
    private readonly IDocumentRepository _documents;
    private readonly IVehicleRepository _vehicles;
    private readonly IFileStorageService _storage;
    private readonly IUnitOfWork _uow;

    public DriverReviewService(
        IDriverRepository drivers,
        IDocumentRepository documents,
        IVehicleRepository vehicles,
        IFileStorageService storage,
        IUnitOfWork uow)
    {
        _drivers = drivers;
        _documents = documents;
        _vehicles = vehicles;
        _storage = storage;
        _uow = uow;
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

    public async Task<DriverReviewDetail> SetDriverStatusAsync(Guid driverId, DriverStatus status, CancellationToken ct = default)
    {
        var driver = await _drivers.GetByIdAsync(driverId, ct)
            ?? throw new NotFoundException("Driver", driverId);

        driver.Status = status;
        _drivers.Update(driver);
        await _uow.SaveChangesAsync(ct);

        return await GetDriverAsync(driverId, ct);
    }
}
