using Mapcars.Domain.Common;
using Mapcars.Domain.Enums;

namespace Mapcars.Domain.Entities;

/// <summary>
/// One recorded failure, from any surface — API, web, or either mobile app.
/// Write-mostly: every surface appends, only the admin portal reads.
///
/// Everything except <see cref="Source"/>, <see cref="Level"/> and
/// <see cref="Message"/> is best-effort context. An error that happens before
/// sign-in has no user, one from a mobile app has no HTTP method, and a client
/// may not know its own version — none of that should stop the entry landing.
/// </summary>
public class ErrorLog : BaseEntity
{
    public ErrorSource Source { get; set; }
    public ErrorLevel Level { get; set; } = ErrorLevel.Error;

    public required string Message { get; set; }
    public string? ExceptionType { get; set; }
    public string? StackTrace { get; set; }

    /// <summary>Request path (API/web) or screen route (mobile).</summary>
    public string? Path { get; set; }
    public string? HttpMethod { get; set; }
    public int? StatusCode { get; set; }

    /// <summary>"rider" | "driver" | "admin", when the caller was authenticated.</summary>
    public string? UserType { get; set; }
    public Guid? UserId { get; set; }

    public string? AppVersion { get; set; }
    public string? Platform { get; set; }
    public string? UserAgent { get; set; }
    public string? IpAddress { get; set; }
    public string? CorrelationId { get; set; }

    // ── Triage ────────────────────────────────────────────────────────────────
    public bool IsResolved { get; set; }
    public DateTime? ResolvedAtUtc { get; set; }
    public Guid? ResolvedByAdminId { get; set; }
}
