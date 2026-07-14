using Mapcars.Application.Documents.Dtos;
using Mapcars.Domain.Entities;

namespace Mapcars.Application.Documents.Mapping;

/// <summary>Manual entity &lt;-&gt; DTO mapping (no AutoMapper — explicit and fast).</summary>
public static class DocumentMappings
{
    public static DocumentResponse ToResponse(this Document document) => new(
        document.Id,
        document.Type.ToString(),
        document.OriginalFileName,
        document.ReviewStatus.ToString(),
        document.CreatedAtUtc,
        document.ReviewedAtUtc);
}
