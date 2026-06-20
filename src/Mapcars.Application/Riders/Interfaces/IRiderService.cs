using Mapcars.Application.Riders.Dtos;

namespace Mapcars.Application.Riders.Interfaces;

/// <summary>Rider use-cases (business logic layer surface).</summary>
public interface IRiderService
{
    Task<RiderResponse> CreateAsync(CreateRiderRequest request, CancellationToken ct = default);
    Task<RiderResponse> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<RiderResponse>> ListAsync(CancellationToken ct = default);
}
