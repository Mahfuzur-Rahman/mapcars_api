namespace Mapcars.Application.Posters.Dtos;

/// <summary>
/// Outbound poster representation. Never expose entities directly. No image
/// URL field — the image lives at the predictable public route
/// <c>GET /api/v1/posters/{Id}/image</c>, which the client builds itself.
/// </summary>
public record PosterResponse(
    Guid Id,
    string? Title,
    string? Subtitle,
    string? LinkUrl,
    int SortOrder,
    bool IsActive,
    DateTime CreatedAtUtc);
