namespace Mapcars.Domain.Entities;

public class AdminMenuPermission
{
    public Guid AdminId { get; set; }
    public int MenuId { get; set; }
    public bool IsAllowed { get; set; }

    public Admin Admin { get; set; } = null!;
    public Menu Menu { get; set; } = null!;
}
