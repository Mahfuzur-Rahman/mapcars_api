using Mapcars.Application.Common.Interfaces;
using Mapcars.Domain.Entities;
using Mapcars.Domain.Enums;

namespace Mapcars.Application.Drivers.Interfaces;

public interface IDriverRepository : IGenericRepository<Driver>
{
    Task<bool> EmailExistsAsync(string email, CancellationToken ct = default);
    /// <summary>Drivers filtered by status (all when null), newest first — for admin review queues.</summary>
    Task<IReadOnlyList<Driver>> ListByStatusAsync(DriverStatus? status, CancellationToken ct = default);
    Task<Driver?> FindByEmailAsync(string email, CancellationToken ct = default);
    Task<Driver?> FindByPhoneAsync(string phone, CancellationToken ct = default);
    Task<Driver?> FindByGoogleSubAsync(string googleSub, CancellationToken ct = default);
    Task<bool> PhoneExistsAsync(string phone, CancellationToken ct = default);
    Task<Driver?> FindByNationalIdNumberAsync(string nationalIdNumber, CancellationToken ct = default);
    Task<Driver?> FindByPassportNumberAsync(string passportNumber, CancellationToken ct = default);
}
