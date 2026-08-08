namespace Mapcars.Application.Common.Interfaces;

public interface IEmailService
{
    string ProviderName { get; }

    /// <summary>The address/name a send uses when <see cref="EmailSendOptions"/> doesn't override it.</summary>
    string DefaultFromAddress { get; }
    string DefaultFromName { get; }

    Task SendAsync(
        string toEmail,
        string subject,
        string body,
        EmailSendOptions? options = null,
        CancellationToken ct = default);
}

/// <summary>
/// Per-send overrides. Every field is optional so existing callers
/// (<c>OtpService</c>, <c>AdminAuthService</c>) keep working unchanged and are
/// logged under the default <see cref="Category"/> of "System".
/// </summary>
public record EmailSendOptions(
    string? FromAddress = null,
    string? FromName = null,
    string Category = "System",
    Guid? SentByAdminId = null);
