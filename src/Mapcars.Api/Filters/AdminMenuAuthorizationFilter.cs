using System.Security.Claims;
using Mapcars.Application.Admins.Interfaces;
using Mapcars.Application.Common.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Mapcars.Api.Filters;

/// <summary>
/// Enforces <see cref="RequiresAdminMenuAttribute"/>. Registered globally next to
/// <see cref="ValidationActionFilter"/> and does nothing on endpoints without the
/// attribute.
/// <para>
/// An MVC authorization filter rather than an authorization policy, because:
/// every admin endpoint is a controller action; it runs before model binding and
/// validation, so a refused caller learns nothing from a 400 and no work is done;
/// and it refuses by throwing <see cref="ForbiddenException"/>, which
/// <c>ExceptionHandlingMiddleware</c> turns into the API's standard problem+json
/// body and an <c>error_logs</c> entry. A policy's failure would instead be the
/// auth handler's empty-bodied 403, and getting problem+json out of that means a
/// custom result handler that would change every existing role-based 403 too.
/// </para>
/// </summary>
public sealed class AdminMenuAuthorizationFilter(IAdminMenuAuthorizer authorizer) : IAsyncAuthorizationFilter
{
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var metadata = context.ActionDescriptor.EndpointMetadata;

        // Controller attributes are listed before the action's, so the last one is
        // the closest to the endpoint — the same "most specific wins" rule
        // EndpointMetadataCollection.GetMetadata<T> applies (e.g. for [EnableCors]).
        var required = metadata.OfType<RequiresAdminMenuAttribute>().LastOrDefault();
        if (required is null || metadata.OfType<IAllowAnonymous>().Any()) return;

        var allowed =
            Guid.TryParse(context.HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier), out var adminId)
            && await authorizer.CanUseAnyAsync(adminId, required.MenuPaths, context.HttpContext.RequestAborted);

        if (!allowed)
            throw new ForbiddenException("You don't have access to this part of the admin portal.");
    }
}
