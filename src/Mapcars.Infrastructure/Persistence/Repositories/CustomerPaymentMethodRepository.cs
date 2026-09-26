using Mapcars.Application.Payments.Interfaces;
using Mapcars.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Mapcars.Infrastructure.Persistence.Repositories;

public class CustomerPaymentMethodRepository(AppDbContext db) : ICustomerPaymentMethodRepository
{
    public async Task<CustomerPaymentMethod?> GetChargeableAsync(Guid customerId, CancellationToken ct = default)
    {
        // Expiry is evaluated in memory: IsUsable/IsExpired are C# rules (end of
        // the expiry month), and a customer has a handful of cards at most.
        var active = await db.CustomerPaymentMethods.AsNoTracking()
            .Where(m => m.CustomerId == customerId && m.IsActive)
            .ToListAsync(ct);

        return active
            .Where(m => m.IsUsable)
            .OrderByDescending(m => m.IsDefault)
            .ThenByDescending(m => m.CreatedAtUtc)
            .FirstOrDefault();
    }
}
