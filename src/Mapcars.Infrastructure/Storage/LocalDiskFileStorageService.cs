using Mapcars.Application.Common.Files;
using Mapcars.Application.Common.Interfaces;
using Mapcars.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace Mapcars.Infrastructure.Storage;

/// <summary>Writes uploaded files to local disk. Swap for an S3-backed implementation later.</summary>
public class LocalDiskFileStorageService : IFileStorageService
{
    private readonly string _basePath;

    public LocalDiskFileStorageService(IOptions<StorageOptions> options)
    {
        _basePath = Path.GetFullPath(options.Value.LocalPath);
        Directory.CreateDirectory(_basePath);
    }

    public async Task<string> SaveAsync(Stream content, string originalFileName, string contentType, CancellationToken ct = default)
    {
        // Never build the key from the raw filename — use a random name plus a
        // vetted extension so a caller can't influence the on-disk path.
        var extension = FileUploadPolicy.SafeExtension(originalFileName);
        var key = $"{Guid.NewGuid():N}{extension}";

        await using var fileStream = File.Create(Path.Combine(_basePath, key));
        await content.CopyToAsync(fileStream, ct);

        return key;
    }

    public Task<Stream?> OpenReadAsync(string storageKey, CancellationToken ct = default)
    {
        // Defence in depth: resolve the key against the base dir and confirm it
        // stays inside it, so a crafted key (e.g. "../../appsettings.json")
        // can't escape the storage root.
        var path = Path.GetFullPath(Path.Combine(_basePath, storageKey));
        if (!path.StartsWith(_basePath, StringComparison.Ordinal))
            return Task.FromResult<Stream?>(null);

        Stream? stream = File.Exists(path) ? File.OpenRead(path) : null;
        return Task.FromResult(stream);
    }
}
