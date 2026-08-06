namespace Mapcars.Application.Posters.Dtos;

/// <summary>Metadata-only edit — swapping the image is a separate endpoint.</summary>
public record UpdatePosterRequest(
    string? Title,
    string? Subtitle,
    string? LinkUrl,
    int SortOrder,
    bool IsActive);
