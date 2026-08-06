using Mapcars.Application.Common.Interfaces;
using Mapcars.Domain.Entities;

namespace Mapcars.Application.Documents.Interfaces;

public interface IDocumentRepository : IGenericRepository<Document>
{
    Task<IReadOnlyList<Document>> ListForRiderAsync(Guid riderId, CancellationToken ct = default);
    Task<IReadOnlyList<Document>> ListForDriverAsync(Guid driverId, CancellationToken ct = default);
    /// <summary>Documents belonging to any of the given drivers — one query, for admin list views.</summary>
    Task<IReadOnlyList<Document>> ListForDriversAsync(IReadOnlyCollection<Guid> driverIds, CancellationToken ct = default);
}
