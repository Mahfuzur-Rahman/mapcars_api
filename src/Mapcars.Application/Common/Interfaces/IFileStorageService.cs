namespace Mapcars.Application.Common.Interfaces;

/// <summary>
/// Stores uploaded files behind a provider-agnostic interface — same pattern
/// as the Mapbox provider seam. The current implementation writes to local
/// disk; swap in an S3-backed implementation later without touching callers.
/// </summary>
public interface IFileStorageService
{
    /// <summary>
    /// Persists the stream under a generated key and returns that key. The
    /// content-type is stored as object metadata so it can be echoed back on
    /// download (the key itself is opaque and reveals nothing about the file).
    /// </summary>
    Task<string> SaveAsync(Stream content, string originalFileName, string contentType, CancellationToken ct = default);

    /// <summary>Opens the file for the given storage key, or null if it doesn't exist.</summary>
    Task<Stream?> OpenReadAsync(string storageKey, CancellationToken ct = default);
}
