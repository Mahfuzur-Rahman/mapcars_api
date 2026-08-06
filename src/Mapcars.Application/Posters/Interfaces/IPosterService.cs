using Mapcars.Application.Posters.Dtos;

namespace Mapcars.Application.Posters.Interfaces;

/// <summary>Poster use-cases (business logic layer surface).</summary>
public interface IPosterService
{
    Task<PosterResponse> CreateAsync(
        CreatePosterRequest request,
        Stream content,
        string fileName,
        string contentType,
        long fileSize,
        Guid? createdByAdminId,
        CancellationToken ct = default);

    Task<PosterResponse> UpdateAsync(Guid id, UpdatePosterRequest request, CancellationToken ct = default);

    Task<PosterResponse> ReplaceImageAsync(
        Guid id, Stream content, string fileName, string contentType, long fileSize, CancellationToken ct = default);

    Task DeleteAsync(Guid id, CancellationToken ct = default);

    /// <summary>Admin list — every poster, active or not, ordered by SortOrder.</summary>
    Task<IReadOnlyList<PosterResponse>> ListAllAsync(CancellationToken ct = default);

    /// <summary>Public list — active posters only, ordered by SortOrder. Feeds the landing page.</summary>
    Task<IReadOnlyList<PosterResponse>> ListActiveAsync(CancellationToken ct = default);

    /// <summary>Opens the stored image for streaming, or null if the poster/file doesn't exist.</summary>
    Task<(Stream Content, string ContentType)?> OpenImageAsync(Guid id, CancellationToken ct = default);
}
