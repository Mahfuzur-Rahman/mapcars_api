using Mapcars.Application.Documents.Dtos;
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
}
