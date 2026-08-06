using Mapcars.Application.Posters.Interfaces;
using Mapcars.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Mapcars.Infrastructure.Persistence.Repositories;

public class PosterRepository : GenericRepository<Poster>, IPosterRepository
{
    public PosterRepository(AppDbContext context) : base(context) { }

    public async Task<IReadOnlyList<Poster>> ListAllOrderedAsync(CancellationToken ct = default)
        => await Set.AsNoTracking().OrderBy(p => p.SortOrder).ToListAsync(ct);

    public async Task<IReadOnlyList<Poster>> ListActiveOrderedAsync(CancellationToken ct = default)
        => await Set.AsNoTracking()
            .Where(p => p.IsActive)
            .OrderBy(p => p.SortOrder)
            .ToListAsync(ct);
}
