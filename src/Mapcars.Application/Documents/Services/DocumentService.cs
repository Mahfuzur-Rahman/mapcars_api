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
        [DocumentType.PhvLicence, DocumentType.VehicleInsurance, DocumentType.VehicleRegistration, DocumentType.DbsCheck];

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
        CancellationToken ct = default)
    {
        var allowedTypes = userType == "rider" ? RiderTypes : DriverTypes;
        if (!allowedTypes.Contains(type))
            throw new DomainException($"Document type '{type}' is not valid for a {userType}.");

        var storageKey = await _storage.SaveAsync(content, originalFileName, ct);

        var document = new Document
        {
            RiderId = userType == "rider" ? userId : null,
            DriverId = userType == "driver" ? userId : null,
            Type = type,
            StorageKey = storageKey,
            OriginalFileName = originalFileName,
            ContentType = contentType,
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
