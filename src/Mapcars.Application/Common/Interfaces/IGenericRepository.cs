using System.Linq.Expressions;
using Mapcars.Domain.Common;

namespace Mapcars.Application.Common.Interfaces;

/// <summary>
/// Generic persistence abstraction. Defined in the Application layer so business
/// logic never depends on EF Core directly — Infrastructure implements it.
/// </summary>
public interface IGenericRepository<T> where T : BaseEntity
{
    Task<T?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<T>> ListAsync(CancellationToken ct = default);
    Task<IReadOnlyList<T>> FindAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default);
    Task AddAsync(T entity, CancellationToken ct = default);
    void Update(T entity);
    void Remove(T entity);
}
