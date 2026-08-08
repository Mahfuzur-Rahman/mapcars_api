using Mapcars.Application.Common.Exceptions;
using Mapcars.Application.ErrorLogs.Dtos;
using Mapcars.Application.ErrorLogs.Interfaces;
using Mapcars.Domain.Entities;
using Mapcars.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Mapcars.Application.ErrorLogs.Services;

/// <summary>
/// Writes and reads the central error log. Two rules shape this class:
///
/// 1. <b>Logging never throws.</b> Every write path swallows its own failure
///    (after telling ILogger) — if the database is the thing that's broken,
///    trying to record that in the database must not replace a useful error
///    with a confusing one.
/// 2. <b>Client input is untrusted.</b> Anyone can POST to the report endpoint,
///    so every string is length-capped and the enums are parsed rather than
///    cast. Identity and IP come from the request context, never the payload.
/// </summary>
public class ErrorLogService : IErrorLogService
{
    // Caps match the column widths in database/021_error_logs.sql.
    private const int MaxMessage = 2000;
    private const int MaxType = 200;
    private const int MaxStack = 20_000;
    private const int MaxPath = 500;
    private const int MaxShort = 50;
    private const int MaxUserAgent = 500;
    private const int MaxCorrelation = 100;

    private const int MaxPageSize = 200;

    private readonly IErrorLogRepository _repo;
    private readonly ILogger<ErrorLogService> _logger;

    public ErrorLogService(IErrorLogRepository repo, ILogger<ErrorLogService> logger)
    {
        _repo = repo;
        _logger = logger;
    }

    public async Task LogAsync(ErrorLog log, CancellationToken ct = default)
    {
        try
        {
            log.Message = Trim(log.Message, MaxMessage) ?? "(no message)";
            log.ExceptionType = Trim(log.ExceptionType, MaxType);
            log.StackTrace = Trim(log.StackTrace, MaxStack);
            log.Path = Trim(log.Path, MaxPath);
            log.AppVersion = Trim(log.AppVersion, MaxShort);
            log.Platform = Trim(log.Platform, MaxShort);
            log.UserAgent = Trim(log.UserAgent, MaxUserAgent);
            log.CorrelationId = Trim(log.CorrelationId, MaxCorrelation);

            await _repo.AddAsync(log, ct);
        }
        catch (Exception ex)
        {
            // Last resort: the console/file logger still has it.
            _logger.LogError(ex, "Failed to persist an error log entry: {Message}", log.Message);
        }
    }

    public Task ReportAsync(ReportErrorRequest request, ErrorRequestContext context, CancellationToken ct = default)
        => LogAsync(new ErrorLog
        {
            Source = ParseSource(request.Source),
            Level = ParseLevel(request.Level),
            Message = request.Message,
            ExceptionType = request.ExceptionType,
            StackTrace = request.StackTrace,
            Path = request.Path,
            AppVersion = request.AppVersion,
            Platform = request.Platform,
            CorrelationId = request.CorrelationId,
            // Never from the payload — the client doesn't get to claim these.
            UserType = context.UserType,
            UserId = context.UserId,
            UserAgent = context.UserAgent,
            IpAddress = context.IpAddress,
        }, ct);

    public async Task<ErrorLogPage> ListAsync(
        string? source, string? level, bool? isResolved, string? search,
        int page, int pageSize, CancellationToken ct = default)
    {
        page = page < 1 ? 1 : page;
        pageSize = Math.Clamp(pageSize < 1 ? 50 : pageSize, 1, MaxPageSize);

        var (items, total) = await _repo.ListAsync(
            TryParseSource(source), TryParseLevel(level), isResolved,
            string.IsNullOrWhiteSpace(search) ? null : search.Trim(),
            page, pageSize, ct);

        return new ErrorLogPage(items.Select(ToListItem).ToList(), total, page, pageSize);
    }

    public async Task<ErrorLogDetail> GetAsync(Guid id, CancellationToken ct = default)
    {
        var log = await _repo.GetByIdAsync(id, ct) ?? throw new NotFoundException("ErrorLog", id);
        return new ErrorLogDetail(
            log.Id, log.Source.ToString(), log.Level.ToString(), log.Message,
            log.ExceptionType, log.StackTrace, log.Path, log.HttpMethod, log.StatusCode,
            log.UserType, log.UserId, log.AppVersion, log.Platform, log.UserAgent,
            log.IpAddress, log.CorrelationId, log.IsResolved, log.ResolvedAtUtc, log.CreatedAtUtc);
    }

    public async Task<ErrorLogSummary> SummaryAsync(CancellationToken ct = default)
    {
        var (total, unresolved, lastDay, errors, warnings) = await _repo.SummaryAsync(ct);
        return new ErrorLogSummary(total, unresolved, lastDay, errors, warnings);
    }

    public Task<bool> SetResolvedAsync(Guid id, bool resolved, Guid? adminId, CancellationToken ct = default)
        => _repo.SetResolvedAsync(id, resolved, adminId, ct);

    public Task<int> PurgeAsync(int olderThanDays, CancellationToken ct = default)
        => _repo.PurgeAsync(DateTime.UtcNow.AddDays(-Math.Max(olderThanDays, 1)), ct);

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static ErrorLogListItem ToListItem(ErrorLog l) => new(
        l.Id, l.Source.ToString(), l.Level.ToString(), l.Message, l.ExceptionType,
        l.Path, l.StatusCode, l.UserType, l.IsResolved, l.CreatedAtUtc);

    private static string? Trim(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        value = value.Trim();
        return value.Length <= max ? value : value[..max];
    }

    /// <summary>An unrecognised source is still worth keeping — file it under Web.</summary>
    private static ErrorSource ParseSource(string? value) => TryParseSource(value) ?? ErrorSource.Web;

    private static ErrorLevel ParseLevel(string? value) => TryParseLevel(value) ?? ErrorLevel.Error;

    private static ErrorSource? TryParseSource(string? value) =>
        Enum.TryParse<ErrorSource>(value, ignoreCase: true, out var parsed) ? parsed : null;

    private static ErrorLevel? TryParseLevel(string? value) =>
        Enum.TryParse<ErrorLevel>(value, ignoreCase: true, out var parsed) ? parsed : null;
}
