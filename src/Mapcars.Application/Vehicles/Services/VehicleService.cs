using Mapcars.Application.Common.Interfaces;
using Mapcars.Application.Vehicles.Dtos;
using Mapcars.Application.Vehicles.Interfaces;
using Mapcars.Application.Vehicles.Mapping;
using Mapcars.Domain.Entities;
using Mapcars.Domain.Exceptions;

namespace Mapcars.Application.Vehicles.Services;

/// <summary>
/// Business logic for a driver's vehicle. Request-shape validation happens at
/// the API boundary; this layer enforces the business rules (one vehicle per
/// driver, plate not already claimed by another driver).
/// </summary>
public class VehicleService : IVehicleService
{
    private readonly IVehicleRepository _vehicles;
    private readonly IUnitOfWork _uow;

    public VehicleService(IVehicleRepository vehicles, IUnitOfWork uow)
    {
        _vehicles = vehicles;
        _uow = uow;
    }

    public async Task<VehicleResponse?> GetForDriverAsync(Guid driverId, CancellationToken ct = default)
    {
        var vehicle = await _vehicles.GetByDriverAsync(driverId, ct);
        return vehicle?.ToResponse();
    }

    public async Task<VehicleResponse> UpsertForDriverAsync(
        Guid driverId, UpsertVehicleRequest request, CancellationToken ct = default)
    {
        // Normalise the plate (upper-cased, no spaces) so uniqueness is reliable.
        var registration = request.RegistrationNumber.Replace(" ", string.Empty).ToUpperInvariant();

        var owner = await _vehicles.GetByRegistrationAsync(registration, ct);
        if (owner is not null && owner.DriverId != driverId)
            throw new DomainException("That registration number is already registered to another driver.");

        var vehicle = await _vehicles.GetByDriverAsync(driverId, ct);
        if (vehicle is null)
        {
            vehicle = new Vehicle
            {
                DriverId = driverId,
                Make = request.Make.Trim(),
                Model = request.Model.Trim(),
                Year = request.Year,
                Colour = request.Colour.Trim(),
                RegistrationNumber = registration,
                PhvLicencePlateNumber = request.PhvLicencePlateNumber?.Trim(),
                PhvLicensingAuthority = request.PhvLicensingAuthority?.Trim(),
            };
            await _vehicles.AddAsync(vehicle, ct);
        }
        else
        {
            vehicle.Make = request.Make.Trim();
            vehicle.Model = request.Model.Trim();
            vehicle.Year = request.Year;
            vehicle.Colour = request.Colour.Trim();
            vehicle.RegistrationNumber = registration;
            vehicle.PhvLicencePlateNumber = request.PhvLicencePlateNumber?.Trim();
            vehicle.PhvLicensingAuthority = request.PhvLicensingAuthority?.Trim();
            _vehicles.Update(vehicle);
        }

        await _uow.SaveChangesAsync(ct);
        return vehicle.ToResponse();
    }
}
