namespace Mapcars.Application.ErrorLogs.Dtos;

/// <summary>
/// What a client (web / customer app / driver app) posts when something breaks
/// on its side. Only <see cref="Source"/> and <see cref="Message"/> are needed —
/// a crash report that arrives with nothing else is still worth having.
/// </summary>
public record ReportErrorRequest(
    string Source,
    string Message,
    string? Level = null,
    string? ExceptionType = null,
    string? StackTrace = null,
    string? Path = null,
    string? AppVersion = null,
    string? Platform = null,
    string? CorrelationId = null);

/// <summary>A row in the admin error list — enough to triage, not the full trace.</summary>
public record ErrorLogListItem(
    Guid Id,
    string Source,
    string Level,
    string Message,
    string? ExceptionType,
    string? Path,
    int? StatusCode,
    string? UserType,
    bool IsResolved,
    DateTime CreatedAtUtc);

/// <summary>The full entry, including the stack trace.</summary>
public record ErrorLogDetail(
    Guid Id,
    string Source,
    string Level,
    string Message,
    string? ExceptionType,
    string? StackTrace,
    string? Path,
    string? HttpMethod,
    int? StatusCode,
    string? UserType,
    Guid? UserId,
    string? AppVersion,
    string? Platform,
    string? UserAgent,
    string? IpAddress,
    string? CorrelationId,
    bool IsResolved,
    DateTime? ResolvedAtUtc,
    DateTime CreatedAtUtc);

/// <summary>One page of errors plus the total, so the UI can page.</summary>
public record ErrorLogPage(IReadOnlyList<ErrorLogListItem> Items, int Total, int Page, int PageSize);

/// <summary>Counts for the header strip (last 24h + unresolved).</summary>
public record ErrorLogSummary(int Total, int Unresolved, int LastDay, int ErrorLevel, int WarningLevel);
