using Mapcars.Application.Common.Interfaces;
using Mapcars.Domain.Entities;

namespace Mapcars.Application.Posters.Interfaces;

public interface IPosterRepository : IGenericRepository<Poster>
{
    Task<IReadOnlyList<Poster>> ListAllOrderedAsync(CancellationToken ct = default);
    Task<IReadOnlyList<Poster>> ListActiveOrderedAsync(CancellationToken ct = default);
}
