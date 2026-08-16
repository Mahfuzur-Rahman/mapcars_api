using Mapcars.Application.Common.Exceptions;
using Mapcars.Application.Common.Interfaces;
using Mapcars.Application.DriverReview.Dtos;
using Mapcars.Application.Vehicles.Dtos;
using Mapcars.Application.Vehicles.Interfaces;
using Mapcars.Application.Vehicles.Mapping;
using Mapcars.Domain.Entities;
using Mapcars.Domain.Enums;
using Mapcars.Domain.Exceptions;

namespace Mapcars.Application.Vehicles.Services;

/// <summary>
/// Business logic for a driver's vehicle and tier appeals. Request-shape validation happens at
/// the API boundary; this layer enforces the business rules.
/// </summary>
public class VehicleService : IVehicleService
{
    private static readonly HashSet<string> AllowedTiers = new(StringComparer.OrdinalIgnoreCase)
    {
        "economy", "comfort", "xl", "premium"
    };

    private readonly IVehicleRepository _vehicles;
    private readonly IVehicleTierAppealRepository _appeals;
    private readonly IFileStorageService _storage;
    private readonly IUnitOfWork _uow;

    public VehicleService(
        IVehicleRepository vehicles,
        IVehicleTierAppealRepository appeals,
        IFileStorageService storage,
        IUnitOfWork uow)
    {
        _vehicles = vehicles;
        _appeals = appeals;
        _storage = storage;
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
                Tier = "economy", // Default tier for new vehicles
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
            // Preserve existing Tier unless updated via admin workflow
            _vehicles.Update(vehicle);
        }

        await _uow.SaveChangesAsync(ct);
        return vehicle.ToResponse();
    }

    public async Task<VehicleTierAppealResponse> SubmitTierAppealAsync(
        Guid driverId,
        string requestedTier,
        string reason,
        List<(Stream Stream, string ContentType, string FileName)>? photos = null,
        CancellationToken ct = default)
    {
        var vehicle = await _vehicles.GetByDriverAsync(driverId, ct)
            ?? throw new DomainException("You must register a vehicle before submitting a tier appeal.");

        var normalisedRequestedTier = requestedTier.Trim().ToLowerInvariant();
        if (!AllowedTiers.Contains(normalisedRequestedTier))
            throw new DomainException($"Invalid tier '{requestedTier}'. Allowed tiers are: {string.Join(", ", AllowedTiers)}");

        if (string.Equals(vehicle.Tier, normalisedRequestedTier, StringComparison.OrdinalIgnoreCase))
            throw new DomainException($"Your vehicle is already in the '{vehicle.Tier}' tier.");

        if (string.IsNullOrWhiteSpace(reason))
            throw new DomainException("Please provide a reason or justification for the tier appeal.");

        var existingPending = await _appeals.GetActivePendingForDriverAsync(driverId, ct);
        if (existingPending is not null)
            throw new DomainException("You already have an appeal pending admin review.");

        var appealId = Guid.NewGuid();
        var storageKeys = new List<string>();

        if (photos is { Count: > 0 })
        {
            for (var i = 0; i < photos.Count; i++)
            {
                var photo = photos[i];
                var key = await _storage.SaveAsync(photo.Stream, photo.FileName, photo.ContentType, ct);
                storageKeys.Add(key);
            }
        }

        var appeal = new VehicleTierAppeal
        {
            Id = appealId,
            DriverId = driverId,
            VehicleId = vehicle.Id,
            CurrentTier = vehicle.Tier,
            RequestedTier = normalisedRequestedTier,
            Reason = reason.Trim(),
            PhotoStorageKeys = storageKeys,
            Status = TierAppealStatus.Pending,
        };

        await _appeals.AddAsync(appeal, ct);
        await _uow.SaveChangesAsync(ct);

        return appeal.ToResponse();
    }

    public async Task<IReadOnlyList<VehicleTierAppealResponse>> ListAppealsForDriverAsync(
        Guid driverId, CancellationToken ct = default)
    {
        var appeals = await _appeals.ListForDriverAsync(driverId, ct);
        return appeals.Select(a => a.ToResponse()).ToList();
    }

    public async Task<VehicleTierAppealResponse?> GetActiveAppealForDriverAsync(
        Guid driverId, CancellationToken ct = default)
    {
        var appeal = await _appeals.GetActivePendingForDriverAsync(driverId, ct);
        return appeal?.ToResponse();
    }

    public async Task<FileContent?> GetAppealPhotoContentAsync(
        Guid driverId, Guid appealId, int photoIndex, CancellationToken ct = default)
    {
        var appeal = await _appeals.GetByIdAsync(appealId, ct);
        if (appeal is null || appeal.DriverId != driverId) return null;

        if (appeal.PhotoStorageKeys is null || photoIndex < 0 || photoIndex >= appeal.PhotoStorageKeys.Count)
            return null;

        var key = appeal.PhotoStorageKeys[photoIndex];
        var stream = await _storage.OpenReadAsync(key, ct);
        if (stream is null) return null;

        return new FileContent(stream, "image/jpeg", Path.GetFileName(key));
    }
}
