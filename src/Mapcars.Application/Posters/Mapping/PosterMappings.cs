using Mapcars.Application.Posters.Dtos;
using Mapcars.Domain.Entities;

namespace Mapcars.Application.Posters.Mapping;

/// <summary>Manual entity &lt;-&gt; DTO mapping (no AutoMapper — explicit and fast).</summary>
public static class PosterMappings
{
    public static PosterResponse ToResponse(this Poster poster) => new(
        poster.Id,
        poster.Title,
        poster.Subtitle,
        poster.LinkUrl,
        poster.SortOrder,
        poster.IsActive,
        poster.CreatedAtUtc);
}
