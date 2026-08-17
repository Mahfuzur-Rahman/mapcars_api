using Mapcars.Application.Documents.Dtos;
using Mapcars.Application.DriverReview.Dtos;
using Mapcars.Domain.Enums;

namespace Mapcars.Application.Documents.Interfaces;

/// <summary>Document use-cases (business logic layer surface).</summary>
public interface IDocumentService
{
    Task<DocumentResponse> UploadAsync(
        string userType,
        Guid userId,
        DocumentType type,
        Stream content,
        string originalFileName,
        string contentType,
        long fileSize,
        DateOnly? expiresOn = null,
        CancellationToken ct = default);

    Task<IReadOnlyList<DocumentResponse>> ListAsync(
        string userType, Guid userId, CancellationToken ct = default);

    Task<FileContent?> GetContentAsync(
        string userType, Guid userId, Guid documentId, CancellationToken ct = default);

    Task<DocumentResponse> RequestDeletionAsync(
        string userType, Guid userId, Guid documentId, string? reason, CancellationToken ct = default);
}
