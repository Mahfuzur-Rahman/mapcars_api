namespace Mapcars.Application.Common.Exceptions;

/// <summary>
/// The caller is signed in but not allowed to do this — a 403, as opposed to
/// <see cref="UnauthorizedException"/>'s 401 ("who are you?").
/// </summary>
public class ForbiddenException(string message) : Exception(message);
