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
        vehicle.Tier,
        vehicle.CreatedAtUtc,
        vehicle.UpdatedAtUtc);

    public static VehicleTierAppealResponse ToResponse(this VehicleTierAppeal appeal, IReadOnlyList<string>? photoUrls = null) => new(
        appeal.Id,
        appeal.DriverId,
        appeal.VehicleId,
        appeal.CurrentTier,
        appeal.RequestedTier,
        appeal.Reason,
        photoUrls ?? appeal.PhotoStorageKeys.Select((_, idx) => $"/api/v1/vehicles/me/appeals/{appeal.Id}/photos/{idx}").ToList(),
        appeal.Status.ToString(),
        appeal.AdminNotes,
        appeal.ReviewedAtUtc,
        appeal.CreatedAtUtc);

    public static TierAppealListItem ToListItem(this VehicleTierAppeal appeal) => new(
        appeal.Id,
        appeal.DriverId,
        appeal.Driver?.FullName,
        appeal.Driver?.Email,
        appeal.Driver?.PhoneNumber,
        appeal.VehicleId,
        appeal.Vehicle != null ? $"{appeal.Vehicle.Make} {appeal.Vehicle.Model} ({appeal.Vehicle.Year})" : "Vehicle",
        appeal.Vehicle?.RegistrationNumber ?? string.Empty,
        appeal.CurrentTier,
        appeal.RequestedTier,
        appeal.Reason,
        appeal.PhotoStorageKeys?.Count ?? 0,
        appeal.Status.ToString(),
        appeal.AdminNotes,
        appeal.ReviewedAtUtc,
        appeal.CreatedAtUtc);
}
