using System.Net;
using System.Text.Json;
using Mapcars.Application.Common.Exceptions;
using Mapcars.Domain.Exceptions;

namespace Mapcars.Api.Middleware;

/// <summary>
/// Central exception → HTTP translation. Keeps controllers free of try/catch and
/// guarantees a consistent problem+json error shape for all clients.
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
            await WriteProblemAsync(context, HttpStatusCode.BadRequest, "Validation failed", ex.Errors);
        }
        catch (UnauthorizedException ex)
        {
            await WriteProblemAsync(context, HttpStatusCode.Unauthorized, ex.Message);
        }
        catch (NotFoundException ex)
        {
            await WriteProblemAsync(context, HttpStatusCode.NotFound, ex.Message);
        }
        catch (DomainException ex)
        {
            await WriteProblemAsync(context, HttpStatusCode.BadRequest, ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception");
            await WriteProblemAsync(context, HttpStatusCode.InternalServerError, "An unexpected error occurred.");
        }
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
