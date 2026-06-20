using Mapcars.Application.Common.Interfaces;
using Mapcars.Domain.Entities;

namespace Mapcars.Application.Drivers.Interfaces;

public interface IDriverRepository : IGenericRepository<Driver>
{
    Task<bool> EmailExistsAsync(string email, CancellationToken ct = default);
    Task<Driver?> FindByEmailAsync(string email, CancellationToken ct = default);
    Task<Driver?> FindByPhoneAsync(string phone, CancellationToken ct = default);
    Task<Driver?> FindByGoogleSubAsync(string googleSub, CancellationToken ct = default);
    Task<bool> PhoneExistsAsync(string phone, CancellationToken ct = default);
}
