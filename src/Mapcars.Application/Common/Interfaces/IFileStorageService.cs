namespace Mapcars.Application.Common.Interfaces;

/// <summary>
/// Stores uploaded files behind a provider-agnostic interface — same pattern
/// as the Mapbox provider seam. The current implementation writes to local
/// disk; swap in an S3-backed implementation later without touching callers.
/// </summary>
public interface IFileStorageService
{
    /// <summary>Persists the stream under a generated key and returns that key.</summary>
    Task<string> SaveAsync(Stream content, string originalFileName, CancellationToken ct = default);
}
