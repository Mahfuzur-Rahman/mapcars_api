using Mapcars.Domain.Common;

namespace Mapcars.Domain.Entities;

/// <summary>
/// An admin-managed promo banner shown on the public landing page. Display
/// behaviour (static row vs. rotating carousel once more than 3 are active)
/// is a web-app concern — this entity just stores content and display order.
/// </summary>
public class Poster : BaseEntity
{
    public required string StorageKey { get; set; }
    public required string ContentType { get; set; }

    public string? Title { get; set; }
    public string? Subtitle { get; set; }
    public string? LinkUrl { get; set; }

    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;

    public Guid? CreatedByAdminId { get; set; }
}
