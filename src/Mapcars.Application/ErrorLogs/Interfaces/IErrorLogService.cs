using Mapcars.Application.ErrorLogs.Dtos;
using Mapcars.Domain.Entities;

namespace Mapcars.Application.ErrorLogs.Interfaces;

/// <summary>
/// Central error logging. Everything that fails anywhere in Mapcars ends up
/// here: the API writes from its exception middleware, the clients POST their
/// own crashes, and the admin portal reads the lot.
/// </summary>
public interface IErrorLogService
{
    /// <summary>
    /// Records an entry. Never throws — a failure to log must never turn into a
    /// second failure on top of the one being logged.
    /// </summary>
    Task LogAsync(ErrorLog log, CancellationToken ct = default);

    /// <summary>Records a client-reported error, sanitising whatever it sent.</summary>
    Task ReportAsync(ReportErrorRequest request, ErrorRequestContext context, CancellationToken ct = default);

    Task<ErrorLogPage> ListAsync(
        string? source, string? level, bool? isResolved, string? search,
        int page, int pageSize, CancellationToken ct = default);

    Task<ErrorLogDetail> GetAsync(Guid id, CancellationToken ct = default);
    Task<ErrorLogSummary> SummaryAsync(CancellationToken ct = default);
    Task<bool> SetResolvedAsync(Guid id, bool resolved, Guid? adminId, CancellationToken ct = default);
    Task<int> PurgeAsync(int olderThanDays, CancellationToken ct = default);
}

/// <summary>
/// Request-scoped facts the API knows but the client can't be trusted to
/// report about itself (who it's authenticated as, its real IP).
/// </summary>
public record ErrorRequestContext(
    string? UserType = null,
    Guid? UserId = null,
    string? UserAgent = null,
    string? IpAddress = null);
