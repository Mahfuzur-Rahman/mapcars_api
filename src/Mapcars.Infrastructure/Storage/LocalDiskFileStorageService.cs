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

    public async Task<string> SaveAsync(Stream content, string originalFileName, CancellationToken ct = default)
    {
        var extension = Path.GetExtension(originalFileName);
        var key = $"{Guid.NewGuid()}{extension}";

        await using var fileStream = File.Create(Path.Combine(_basePath, key));
        await content.CopyToAsync(fileStream, ct);

        return key;
    }
}
