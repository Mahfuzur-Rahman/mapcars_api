using Mapcars.Application.Common.Files;
using Mapcars.Application.Common.Interfaces;
using Mapcars.Application.Documents.Dtos;
using Mapcars.Application.Documents.Interfaces;
using Mapcars.Application.Documents.Mapping;
using Mapcars.Domain.Entities;
using Mapcars.Domain.Enums;
using Mapcars.Domain.Exceptions;

namespace Mapcars.Application.Documents.Services;

/// <summary>
/// Business logic for documents. Which DocumentType values are valid depends
/// on the uploader's role (rider identity docs vs. driver licensing docs) —
/// that mapping is a business rule, so it's enforced here rather than at the
/// request-shape validation layer.
/// </summary>
public class DocumentService : IDocumentService
{
    private static readonly HashSet<DocumentType> RiderTypes =
        [DocumentType.ProofOfIdentity, DocumentType.ProofOfAddress];

    private static readonly HashSet<DocumentType> DriverTypes =
    [
        DocumentType.PhvLicence, DocumentType.VehicleInsurance, DocumentType.VehicleRegistration, DocumentType.DbsCheck,
        DocumentType.VehicleFrontPhoto, DocumentType.VehicleRearPhoto, DocumentType.VehicleInteriorPhoto,
        DocumentType.Passport, DocumentType.DrivingLicence, DocumentType.VehicleBadge, DocumentType.BankStatement,
        DocumentType.ProofOfAddress,
    ];

    // These document types carry a renewal deadline — an expiry date is required at upload time.
    private static readonly HashSet<DocumentType> ExpiringTypes =
    [
        DocumentType.PhvLicence, DocumentType.VehicleInsurance, DocumentType.VehicleRegistration, DocumentType.DbsCheck,
        DocumentType.Passport, DocumentType.DrivingLicence, DocumentType.VehicleBadge,
    ];

    private readonly IDocumentRepository _documents;
    private readonly IFileStorageService _storage;
    private readonly IUnitOfWork _uow;

    public DocumentService(IDocumentRepository documents, IFileStorageService storage, IUnitOfWork uow)
    {
        _documents = documents;
        _storage = storage;
        _uow = uow;
    }

    public async Task<DocumentResponse> UploadAsync(
        string userType,
        Guid userId,
        DocumentType type,
        Stream content,
        string originalFileName,
        string contentType,
        long fileSize,
        DateOnly? expiresOn = null,
        CancellationToken ct = default)
    {
        var allowedTypes = userType == "rider" ? RiderTypes : DriverTypes;
        if (!allowedTypes.Contains(type))
            throw new DomainException($"Document type '{type}' is not valid for a {userType}.");

        if (ExpiringTypes.Contains(type) && expiresOn is null)
            throw new DomainException($"An expiry date is required for document type '{type}'.");

        // Security gate: allowlist content-type/extension + hard size cap.
        FileUploadPolicy.EnsureValidDocument(contentType, originalFileName, fileSize);

        var storageKey = await _storage.SaveAsync(content, originalFileName, contentType, ct);

        var document = new Document
        {
            RiderId = userType == "rider" ? userId : null,
            DriverId = userType == "driver" ? userId : null,
            Type = type,
            StorageKey = storageKey,
            OriginalFileName = originalFileName,
            ContentType = contentType,
            ExpiresOn = expiresOn,
        };

        await _documents.AddAsync(document, ct);
        await _uow.SaveChangesAsync(ct);

        return document.ToResponse();
    }

    public async Task<IReadOnlyList<DocumentResponse>> ListAsync(
        string userType, Guid userId, CancellationToken ct = default)
    {
        var documents = userType == "rider"
            ? await _documents.ListForRiderAsync(userId, ct)
            : await _documents.ListForDriverAsync(userId, ct);

        return documents.Select(d => d.ToResponse()).ToList();
    }
}
