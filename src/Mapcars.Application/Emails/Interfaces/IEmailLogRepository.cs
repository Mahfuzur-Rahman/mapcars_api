using Mapcars.Domain.Entities;

namespace Mapcars.Application.Emails.Interfaces;

/// <summary>Persistence for <see cref="EmailLog"/> — append + admin reads.</summary>
public interface IEmailLogRepository
{
    /// <summary>
    /// Writes one entry and commits immediately. Deliberately self-contained
    /// (its own SaveChanges), same reasoning as <c>IErrorLogRepository.AddAsync</c>:
    /// a send has just succeeded or failed, and its own ambient transaction (if
    /// any) shouldn't determine whether the record of it survives.
    /// </summary>
    Task AddAsync(EmailLog log, CancellationToken ct = default);

    Task<(IReadOnlyList<EmailLog> Items, int Total)> ListAsync(
        string? category,
        string? status,
        string? search,
        int page,
        int pageSize,
        CancellationToken ct = default);

    Task<EmailLog?> GetByIdAsync(Guid id, CancellationToken ct = default);
}
