using System.Linq.Expressions;
using Mapcars.Application.Common.Interfaces;
using Mapcars.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace Mapcars.Infrastructure.Persistence.Repositories;

/// <summary>EF Core implementation of the generic repository.</summary>
public class GenericRepository<T> : IGenericRepository<T> where T : BaseEntity
{
    protected readonly AppDbContext Context;
    protected readonly DbSet<T> Set;

    public GenericRepository(AppDbContext context)
    {
        Context = context;
        Set = context.Set<T>();
    }

    public async Task<T?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await Set.FindAsync([id], ct);

    public async Task<IReadOnlyList<T>> ListAsync(CancellationToken ct = default)
        => await Set.AsNoTracking().ToListAsync(ct);

    public async Task<IReadOnlyList<T>> FindAsync(
        Expression<Func<T, bool>> predicate, CancellationToken ct = default)
        => await Set.AsNoTracking().Where(predicate).ToListAsync(ct);

    public async Task AddAsync(T entity, CancellationToken ct = default)
        => await Set.AddAsync(entity, ct);

    public void Update(T entity) => Set.Update(entity);

    public void Remove(T entity) => Set.Remove(entity);
}
