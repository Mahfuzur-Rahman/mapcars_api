using Mapcars.Application.Common.Interfaces;
using Mapcars.Domain.Entities;

namespace Mapcars.Application.Customers.Interfaces;

public interface ICustomerRepository : IGenericRepository<Customer>
{
    Task<bool> EmailExistsAsync(string email, CancellationToken ct = default);
    Task<Customer?> FindByEmailAsync(string email, CancellationToken ct = default);
    Task<Customer?> FindByPhoneAsync(string phone, CancellationToken ct = default);
    Task<Customer?> FindByGoogleSubAsync(string googleSub, CancellationToken ct = default);
    Task<bool> PhoneExistsAsync(string phone, CancellationToken ct = default);
}
