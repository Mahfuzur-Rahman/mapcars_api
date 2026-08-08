using System.Net;
using System.Security.Claims;
using System.Text.Json;
using Mapcars.Application.Common.Exceptions;
using Mapcars.Application.ErrorLogs.Interfaces;
using Mapcars.Domain.Entities;
using Mapcars.Domain.Enums;
using Mapcars.Domain.Exceptions;

namespace Mapcars.Api.Middleware;

/// <summary>
/// Central exception → HTTP translation. Keeps controllers free of try/catch and
/// guarantees a consistent problem+json error shape for all clients.
///
/// It is also where every API failure enters the central error log
/// (<c>error_logs</c>, read from the admin portal's Error Logger page).
/// Unhandled exceptions land as <see cref="ErrorLevel.Error"/>; the handled
/// business rejections (validation, not-found, unauthorised, domain rules) land
/// as <see cref="ErrorLevel.Warning"/> so a wall of ordinary 400s can be
/// filtered away from the 500s that actually need someone's attention.
/// </summary>
public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (ValidationException ex)
        {
            await LogAsync(context, ex, HttpStatusCode.BadRequest, ErrorLevel.Warning, "Validation failed");
            await WriteProblemAsync(context, HttpStatusCode.BadRequest, "Validation failed", ex.Errors);
        }
        catch (UnauthorizedException ex)
        {
            await LogAsync(context, ex, HttpStatusCode.Unauthorized, ErrorLevel.Warning);
            await WriteProblemAsync(context, HttpStatusCode.Unauthorized, ex.Message);
        }
        catch (NotFoundException ex)
        {
            await LogAsync(context, ex, HttpStatusCode.NotFound, ErrorLevel.Warning);
            await WriteProblemAsync(context, HttpStatusCode.NotFound, ex.Message);
        }
        catch (DomainException ex)
        {
            await LogAsync(context, ex, HttpStatusCode.BadRequest, ErrorLevel.Warning);
            await WriteProblemAsync(context, HttpStatusCode.BadRequest, ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception");
            await LogAsync(context, ex, HttpStatusCode.InternalServerError, ErrorLevel.Error);
            await WriteProblemAsync(context, HttpStatusCode.InternalServerError, "An unexpected error occurred.");
        }
    }

    /// <summary>
    /// Appends the failure to <c>error_logs</c>. Resolved from the request scope
    /// rather than injected, because this middleware is a singleton and the
    /// service (and its DbContext) are scoped.
    ///
    /// Never throws: if the log write fails the request must still get its
    /// proper error response, so the original exception isn't replaced by a
    /// logging one.
    /// </summary>
    private async Task LogAsync(
        HttpContext context,
        Exception ex,
        HttpStatusCode status,
        ErrorLevel level,
        string? messageOverride = null)
    {
        try
        {
            var errors = context.RequestServices.GetService<IErrorLogService>();
            if (errors is null) return;

            await errors.LogAsync(new ErrorLog
            {
                Source = ErrorSource.Api,
                Level = level,
                Message = messageOverride ?? ex.Message,
                ExceptionType = ex.GetType().Name,
                // Only the real faults need a trace — a validation warning's
                // stack is noise, and this table is written to on every 400.
                StackTrace = level >= ErrorLevel.Error ? ex.ToString() : null,
                Path = context.Request.Path.HasValue ? context.Request.Path.Value : null,
                HttpMethod = context.Request.Method,
                StatusCode = (int)status,
                UserType = UserTypeOf(context),
                UserId = UserIdOf(context),
                UserAgent = context.Request.Headers.UserAgent.ToString(),
                IpAddress = ClientIp(context),
                CorrelationId = context.TraceIdentifier,
            }, context.RequestAborted);
        }
        catch (Exception logEx)
        {
            _logger.LogError(logEx, "Failed to write to the error log");
        }
    }

    private static string? UserTypeOf(HttpContext context)
    {
        if (context.User.Identity?.IsAuthenticated != true) return null;
        return context.User.FindFirstValue("userType") ?? context.User.FindFirstValue(ClaimTypes.Role);
    }

    private static Guid? UserIdOf(HttpContext context)
        => Guid.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    private static string? ClientIp(HttpContext context)
    {
        var forwarded = context.Request.Headers["X-Forwarded-For"].ToString();
        if (!string.IsNullOrWhiteSpace(forwarded)) return forwarded.Split(',')[0].Trim();
        return context.Connection.RemoteIpAddress?.ToString();
    }

    private static async Task WriteProblemAsync(
        HttpContext context, HttpStatusCode status, string title, object? errors = null)
    {
        context.Response.StatusCode = (int)status;
        context.Response.ContentType = "application/problem+json";

        var payload = new
        {
            title,
            status = (int)status,
            errors
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(payload));
    }
}
