using Mapcars.Application.Riders.Dtos;
using Mapcars.Domain.Entities;

namespace Mapcars.Application.Riders.Mapping;

/// <summary>Manual entity &lt;-&gt; DTO mapping (no AutoMapper — explicit and fast).</summary>
public static class RiderMappings
{
    public static RiderResponse ToResponse(this Rider rider) => new(
        rider.Id,
        rider.FullName ?? string.Empty,
        rider.Email ?? string.Empty,
        rider.PhoneNumber ?? string.Empty,
        rider.IsActive,
        rider.CreatedAtUtc);
}
