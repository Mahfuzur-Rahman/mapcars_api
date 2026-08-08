using Mapcars.Domain.Common;

namespace Mapcars.Domain.Entities;

/// <summary>
/// One recorded send attempt, from any code path. Write-mostly: every call to
/// <c>IEmailService.SendAsync</c> appends one row (via the logging decorator),
/// only the admin portal reads.
/// </summary>
public class EmailLog : BaseEntity
{
    public required string ToEmail { get; set; }
    public required string FromAddress { get; set; }
    public string? FromName { get; set; }

    public required string Subject { get; set; }
    public required string BodyHtml { get; set; }

    /// <summary>"resend" | "smtp" | "console" — whichever provider handled it.</summary>
    public required string Provider { get; set; }

    /// <summary>"System" (OTP, welcome emails, ...) | "Compose" (admin-authored). Free string — room to grow.</summary>
    public string Category { get; set; } = "System";

    /// <summary>"Sent" | "Failed".</summary>
    public required string Status { get; set; }
    public string? ErrorMessage { get; set; }

    /// <summary>Set only for Category = "Compose".</summary>
    public Guid? SentByAdminId { get; set; }
}
