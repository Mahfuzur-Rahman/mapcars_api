using Mapcars.Application.Common.Files;
using Mapcars.Application.Common.Interfaces;
using Mapcars.Application.Posters.Dtos;
using Mapcars.Application.Posters.Interfaces;
using Mapcars.Application.Posters.Mapping;
using Mapcars.Domain.Entities;
using NotFoundException = Mapcars.Application.Common.Exceptions.NotFoundException;

namespace Mapcars.Application.Posters.Services;

/// <summary>
/// Business logic for landing-page posters. Which-posters-are-public is the
/// only business rule (IsActive); static-row-vs-carousel display is a web
/// concern, not enforced here.
/// </summary>
public class PosterService : IPosterService
{
    private readonly IPosterRepository _posters;
    private readonly IFileStorageService _storage;
    private readonly IUnitOfWork _uow;

    public PosterService(IPosterRepository posters, IFileStorageService storage, IUnitOfWork uow)
    {
        _posters = posters;
        _storage = storage;
        _uow = uow;
    }

    public async Task<PosterResponse> CreateAsync(
        CreatePosterRequest request,
        Stream content,
        string fileName,
        string contentType,
        long fileSize,
        Guid? createdByAdminId,
        CancellationToken ct = default)
    {
        FileUploadPolicy.EnsureValidImage(contentType, fileName, fileSize);

        var storageKey = await _storage.SaveAsync(content, fileName, contentType, ct);

        var poster = new Poster
        {
            StorageKey = storageKey,
            ContentType = contentType,
            Title = request.Title,
            Subtitle = request.Subtitle,
            LinkUrl = request.LinkUrl,
            SortOrder = request.SortOrder,
            IsActive = request.IsActive,
            CreatedByAdminId = createdByAdminId,
        };

        await _posters.AddAsync(poster, ct);
        await _uow.SaveChangesAsync(ct);

        return poster.ToResponse();
    }

    public async Task<PosterResponse> UpdateAsync(Guid id, UpdatePosterRequest request, CancellationToken ct = default)
    {
        var poster = await _posters.GetByIdAsync(id, ct) ?? throw new NotFoundException(nameof(Poster), id);

        poster.Title = request.Title;
        poster.Subtitle = request.Subtitle;
        poster.LinkUrl = request.LinkUrl;
        poster.SortOrder = request.SortOrder;
        poster.IsActive = request.IsActive;

        _posters.Update(poster);
        await _uow.SaveChangesAsync(ct);

        return poster.ToResponse();
    }

    public async Task<PosterResponse> ReplaceImageAsync(
        Guid id, Stream content, string fileName, string contentType, long fileSize, CancellationToken ct = default)
    {
        var poster = await _posters.GetByIdAsync(id, ct) ?? throw new NotFoundException(nameof(Poster), id);

        FileUploadPolicy.EnsureValidImage(contentType, fileName, fileSize);

        poster.StorageKey = await _storage.SaveAsync(content, fileName, contentType, ct);
        poster.ContentType = contentType;

        _posters.Update(poster);
        await _uow.SaveChangesAsync(ct);

        return poster.ToResponse();
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var poster = await _posters.GetByIdAsync(id, ct) ?? throw new NotFoundException(nameof(Poster), id);

        _posters.Remove(poster);
        await _uow.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<PosterResponse>> ListAllAsync(CancellationToken ct = default)
    {
        var posters = await _posters.ListAllOrderedAsync(ct);
        return posters.Select(p => p.ToResponse()).ToList();
    }

    public async Task<IReadOnlyList<PosterResponse>> ListActiveAsync(CancellationToken ct = default)
    {
        var posters = await _posters.ListActiveOrderedAsync(ct);
        return posters.Select(p => p.ToResponse()).ToList();
    }

    public async Task<(Stream Content, string ContentType)?> OpenImageAsync(Guid id, CancellationToken ct = default)
    {
        var poster = await _posters.GetByIdAsync(id, ct);
        if (poster is null) return null;

        var stream = await _storage.OpenReadAsync(poster.StorageKey, ct);
        return stream is null ? null : (stream, poster.ContentType);
    }
}
