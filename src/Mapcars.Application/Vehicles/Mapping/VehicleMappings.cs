using Mapcars.Application.Vehicles.Dtos;
using Mapcars.Domain.Entities;

namespace Mapcars.Application.Vehicles.Mapping;

/// <summary>Manual entity &lt;-&gt; DTO mapping (no AutoMapper — explicit and fast).</summary>
public static class VehicleMappings
{
    public static VehicleResponse ToResponse(this Vehicle vehicle) => new(
        vehicle.Id,
        vehicle.Make,
        vehicle.Model,
        vehicle.Year,
        vehicle.Colour,
        vehicle.RegistrationNumber,
        vehicle.PhvLicencePlateNumber,
        vehicle.PhvLicensingAuthority,
        vehicle.CreatedAtUtc,
        vehicle.UpdatedAtUtc);
}
