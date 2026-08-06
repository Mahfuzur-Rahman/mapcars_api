namespace Mapcars.Application.Posters.Dtos;

/// <summary>
/// Multipart create request metadata — bound via [FromForm] in the controller.
/// The image file itself is a separate IFormFile controller parameter (kept
/// out of this Application-layer DTO, which must stay ASP.NET-agnostic).
/// </summary>
public class CreatePosterRequest
{
    public string? Title { get; set; }
    public string? Subtitle { get; set; }
    public string? LinkUrl { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}
