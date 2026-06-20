namespace Mapcars.Application.Admins.Dtos;

public class MenuResponse
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Path { get; set; }
    public string? Icon { get; set; }
    public int? ParentId { get; set; }
    public int SortOrder { get; set; }
    public List<MenuResponse> Children { get; set; } = [];
}
