using Mapcars.Application.Common.Interfaces;
using Mapcars.Application.DriverReview.Dtos;
using Mapcars.Application.Vehicles.Dtos;

namespace Mapcars.Application.Vehicles.Interfaces;

/// <summary>Vehicle use-cases (business logic layer surface).</summary>
public interface IVehicleService
{
    Task<VehicleResponse?> GetForDriverAsync(Guid driverId, CancellationToken ct = default);
    Task<VehicleResponse> UpsertForDriverAsync(Guid driverId, UpsertVehicleRequest request, CancellationToken ct = default);
    Task<VehicleTierAppealResponse> SubmitTierAppealAsync(Guid driverId, string requestedTier, string reason, List<(Stream Stream, string ContentType, string FileName)>? photos = null, CancellationToken ct = default);
    Task<IReadOnlyList<VehicleTierAppealResponse>> ListAppealsForDriverAsync(Guid driverId, CancellationToken ct = default);
    Task<VehicleTierAppealResponse?> GetActiveAppealForDriverAsync(Guid driverId, CancellationToken ct = default);
    Task<FileContent?> GetAppealPhotoContentAsync(Guid driverId, Guid appealId, int photoIndex, CancellationToken ct = default);
}
