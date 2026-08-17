using Mapcars.Application.Documents.Interfaces;
using Mapcars.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Mapcars.Infrastructure.Persistence.Repositories;

public class DocumentRepository : GenericRepository<Document>, IDocumentRepository
{
    public DocumentRepository(AppDbContext context) : base(context) { }

    public async Task<IReadOnlyList<Document>> ListForRiderAsync(Guid riderId, CancellationToken ct = default)
        => await Set.AsNoTracking()
            .Where(d => d.RiderId == riderId)
            .OrderByDescending(d => d.CreatedAtUtc)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Document>> ListForDriverAsync(Guid driverId, CancellationToken ct = default)
        => await Set.AsNoTracking()
            .Where(d => d.DriverId == driverId)
            .OrderByDescending(d => d.CreatedAtUtc)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Document>> ListForDriversAsync(IReadOnlyCollection<Guid> driverIds, CancellationToken ct = default)
        => driverIds.Count == 0
            ? []
            : await Set.AsNoTracking()
                .Where(d => d.DriverId != null && driverIds.Contains(d.DriverId.Value))
                .ToListAsync(ct);

    public async Task<IReadOnlyList<Document>> ListAllDriverDocumentsAsync(Mapcars.Domain.Enums.DocumentReviewStatus? status = null, CancellationToken ct = default)
    {
        var query = Set.AsNoTracking()
            .Include(d => d.Driver)
            .Where(d => d.DriverId != null);

        if (status.HasValue)
            query = query.Where(d => d.ReviewStatus == status.Value);

        return await query
            .OrderByDescending(d => d.CreatedAtUtc)
            .ToListAsync(ct);
    }
}
