using Mapcars.Application.Common.Exceptions;
using Mapcars.Application.Common.Interfaces;
using Mapcars.Application.Emails.Dtos;
using Mapcars.Application.Emails.Interfaces;
using Mapcars.Domain.Entities;

namespace Mapcars.Application.Emails.Services;

/// <summary>
/// Sends Compose mail through the same <see cref="IEmailService"/> path every
/// other send in the app uses — the logging decorator behind it writes the
/// <see cref="EmailLog"/> row, so this class never writes one itself.
/// </summary>
public class EmailAdminService(IEmailService email, IEmailLogRepository logs) : IEmailAdminService
{
    private const int MaxPageSize = 200;

    public Task ComposeAsync(ComposeEmailRequest request, Guid adminId, CancellationToken ct = default)
        => email.SendAsync(
            request.To,
            request.Subject,
            request.BodyHtml,
            new EmailSendOptions(
                FromAddress: request.FromAddress,
                FromName: request.FromName,
                Category: "Compose",
                SentByAdminId: adminId),
            ct);

    public async Task<EmailLogPage> ListAsync(
        string? category, string? status, string? search,
        int page, int pageSize, CancellationToken ct = default)
    {
        page = page < 1 ? 1 : page;
        pageSize = Math.Clamp(pageSize < 1 ? 50 : pageSize, 1, MaxPageSize);

        var (items, total) = await logs.ListAsync(
            string.IsNullOrWhiteSpace(category) ? null : category.Trim(),
            string.IsNullOrWhiteSpace(status) ? null : status.Trim(),
            string.IsNullOrWhiteSpace(search) ? null : search.Trim(),
            page, pageSize, ct);

        return new EmailLogPage(items.Select(ToListItem).ToList(), total, page, pageSize);
    }

    public async Task<EmailLogDetail> GetAsync(Guid id, CancellationToken ct = default)
    {
        var log = await logs.GetByIdAsync(id, ct) ?? throw new NotFoundException("EmailLog", id);
        return new EmailLogDetail(
            log.Id, log.ToEmail, log.FromAddress, log.FromName, log.Subject, log.BodyHtml,
            log.Provider, log.Category, log.Status, log.ErrorMessage, log.SentByAdminId, log.CreatedAtUtc);
    }

    private static EmailLogListItem ToListItem(EmailLog l) => new(
        l.Id, l.ToEmail, l.FromAddress, l.Subject, l.Provider, l.Category, l.Status, l.CreatedAtUtc);
}
