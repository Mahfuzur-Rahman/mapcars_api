using Mapcars.Application.Emails.Dtos;

namespace Mapcars.Application.Emails.Interfaces;

/// <summary>The Email page in the admin portal: send ad-hoc mail, read the log.</summary>
public interface IEmailAdminService
{
    /// <summary>Sends and — via the logging decorator behind <c>IEmailService</c> — logs the attempt.</summary>
    Task ComposeAsync(ComposeEmailRequest request, Guid adminId, CancellationToken ct = default);

    Task<EmailLogPage> ListAsync(
        string? category, string? status, string? search,
        int page, int pageSize, CancellationToken ct = default);

    Task<EmailLogDetail> GetAsync(Guid id, CancellationToken ct = default);
}
