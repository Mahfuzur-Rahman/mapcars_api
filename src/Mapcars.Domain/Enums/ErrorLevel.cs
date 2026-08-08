namespace Mapcars.Domain.Enums;

/// <summary>
/// How serious a logged error is. The split matters for triage: an
/// <see cref="Error"/> is something that shouldn't have happened (an unhandled
/// exception, a 500), whereas a <see cref="Warning"/> is the system correctly
/// rejecting a request (validation, "trip already taken", a failed login) —
/// worth seeing in bulk, not worth waking anyone up for.
/// </summary>
public enum ErrorLevel
{
    Warning = 0,
    Error = 1,
    Fatal = 2,
}
