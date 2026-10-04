namespace Mapcars.Api.Filters;

/// <summary>
/// Gates an admin endpoint on the admin-portal menu(s) whose pages use it, so the
/// API enforces the same menu permissions the portal's sidebar shows. The admin
/// passes if they hold <b>any</b> of the listed menus (identified by
/// <c>menus.path</c> — use the <c>AdminMenuPaths</c> constants); a SuperAdmin
/// always passes; a disabled admin never does. Enforced by
/// <see cref="AdminMenuAuthorizationFilter"/>; a refusal is a 403 problem+json.
/// <para>
/// This only narrows. Keep the role check (<c>[Authorize(Roles = ...)]</c>) on
/// the endpoint as well: it rejects customer/driver tokens up front.
/// </para>
/// <para>
/// May go on a controller or an action. When both carry one, the action's wins
/// (it replaces the controller's list rather than adding to it). Anything marked
/// <c>[AllowAnonymous]</c> is skipped, the same way <c>[Authorize]</c> treats it.
/// </para>
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public sealed class RequiresAdminMenuAttribute(params string[] menuPaths) : Attribute
{
    public IReadOnlyList<string> MenuPaths { get; } = menuPaths;
}
