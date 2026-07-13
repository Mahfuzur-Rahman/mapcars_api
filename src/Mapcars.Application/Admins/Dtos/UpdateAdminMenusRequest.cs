namespace Mapcars.Application.Admins.Dtos;

/// <summary>
/// SuperAdmin sets the complete set of menus an admin may see. The service diffs
/// this against the admin's role defaults and stores only the deltas as overrides.
/// </summary>
public class UpdateAdminMenusRequest
{
    public List<int> MenuIds { get; set; } = [];
}
