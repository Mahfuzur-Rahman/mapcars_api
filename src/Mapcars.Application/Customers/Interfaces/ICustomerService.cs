using Mapcars.Application.Customers.Dtos;

namespace Mapcars.Application.Customers.Interfaces;

/// <summary>Customer use-cases (business logic layer surface).</summary>
public interface ICustomerService
{
    Task<CustomerResponse> CreateAsync(CreateCustomerRequest request, CancellationToken ct = default);
    Task<CustomerResponse> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<CustomerResponse>> ListAsync(CancellationToken ct = default);
}
