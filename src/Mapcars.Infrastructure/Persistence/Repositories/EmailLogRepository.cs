using Mapcars.Application.Emails.Interfaces;
using Mapcars.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Mapcars.Infrastructure.Persistence.Repositories;

/// <summary>
/// Email-log persistence. <see cref="AddAsync"/> commits on its own rather
/// than joining the ambient unit of work — same reasoning as
/// <c>ErrorLogRepository</c>: a send just succeeded or failed, independently
/// of whatever request triggered it.
/// </summary>
public class EmailLogRepository : IEmailLogRepository
{
    private readonly AppDbContext _db;

    public EmailLogRepository(AppDbContext db) => _db = db;

    public async Task AddAsync(EmailLog log, CancellationToken ct = default)
    {
        _db.EmailLogs.Add(log);
        await _db.SaveChangesAsync(ct);
        _db.Entry(log).State = EntityState.Detached;
    }

    public async Task<(IReadOnlyList<EmailLog> Items, int Total)> ListAsync(
        string? category,
        string? status,
        string? search,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var query = _db.EmailLogs.AsNoTracking().AsQueryable();

        if (category is not null) query = query.Where(e => e.Category == category);
        if (status is not null) query = query.Where(e => e.Status == status);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search}%";
            query = query.Where(e =>
                EF.Functions.ILike(e.ToEmail, pattern) ||
                EF.Functions.ILike(e.Subject, pattern) ||
                EF.Functions.ILike(e.FromAddress, pattern));
        }

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(e => e.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, total);
    }

    public async Task<EmailLog?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _db.EmailLogs.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id, ct);
}
