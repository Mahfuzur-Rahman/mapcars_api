using Mapcars.Application.Customers.Interfaces;
using Mapcars.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Mapcars.Infrastructure.Persistence.Repositories;

public class CustomerRepository : GenericRepository<Customer>, ICustomerRepository
{
    public CustomerRepository(AppDbContext context) : base(context) { }

    public Task<bool> EmailExistsAsync(string email, CancellationToken ct = default)
        => Set.AnyAsync(r => r.Email == email, ct);

    public Task<Customer?> FindByEmailAsync(string email, CancellationToken ct = default)
        => Set.FirstOrDefaultAsync(r => r.Email == email.ToLowerInvariant().Trim(), ct);

    public Task<Customer?> FindByPhoneAsync(string phone, CancellationToken ct = default)
        => Set.FirstOrDefaultAsync(r => r.PhoneNumber == phone, ct);

    public Task<Customer?> FindByGoogleSubAsync(string googleSub, CancellationToken ct = default)
        => Set.FirstOrDefaultAsync(r => r.GoogleSub == googleSub, ct);

    public Task<bool> PhoneExistsAsync(string phone, CancellationToken ct = default)
        => Set.AnyAsync(r => r.PhoneNumber == phone, ct);
}
