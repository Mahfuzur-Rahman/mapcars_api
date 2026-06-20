using Mapcars.Application.Common.Interfaces;
using Mapcars.Domain.Entities;

namespace Mapcars.Application.Riders.Interfaces;

public interface IRiderRepository : IGenericRepository<Rider>
{
    Task<bool> EmailExistsAsync(string email, CancellationToken ct = default);
    Task<Rider?> FindByEmailAsync(string email, CancellationToken ct = default);
    Task<Rider?> FindByPhoneAsync(string phone, CancellationToken ct = default);
    Task<Rider?> FindByGoogleSubAsync(string googleSub, CancellationToken ct = default);
    Task<bool> PhoneExistsAsync(string phone, CancellationToken ct = default);
}
