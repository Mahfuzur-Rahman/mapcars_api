using Mapcars.Application.Customers.Dtos;
using Mapcars.Domain.Entities;

namespace Mapcars.Application.Customers.Mapping;

/// <summary>Manual entity &lt;-&gt; DTO mapping (no AutoMapper — explicit and fast).</summary>
public static class CustomerMappings
{
    public static CustomerResponse ToResponse(this Customer customer) => new(
        customer.Id,
        customer.FullName ?? string.Empty,
        customer.Email ?? string.Empty,
        customer.PhoneNumber ?? string.Empty,
        customer.IsActive,
        customer.CreatedAtUtc);
}
