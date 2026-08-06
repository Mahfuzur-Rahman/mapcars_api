using Amazon.S3;
using Amazon.S3.Model;
using Mapcars.Application.Common.Files;
using Mapcars.Application.Common.Interfaces;
using Mapcars.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace Mapcars.Infrastructure.Storage;

/// <summary>
/// Cloudflare R2 storage via the S3-compatible API. The bucket is PRIVATE:
/// objects have no public URL and are never made public. Every read is proxied
/// through an authenticated API endpoint, so credentials stay server-side and
/// access control is enforced per request. Swappable drop-in for
/// <see cref="LocalDiskFileStorageService"/> behind <see cref="IFileStorageService"/>.
/// </summary>
public class R2FileStorageService : IFileStorageService
{
    private readonly IAmazonS3 _s3;
    private readonly string _bucket;

    public R2FileStorageService(IAmazonS3 s3, IOptions<R2Options> options)
    {
        _s3 = s3;
        _bucket = options.Value.BucketName;
    }

    public async Task<string> SaveAsync(Stream content, string originalFileName, string contentType, CancellationToken ct = default)
    {
        // Opaque, unguessable key + vetted extension only — the caller's filename
        // never reaches the object key.
        var key = $"{Guid.NewGuid():N}{FileUploadPolicy.SafeExtension(originalFileName)}";

        var request = new PutObjectRequest
        {
            BucketName = _bucket,
            Key = key,
            InputStream = content,
            ContentType = contentType,
            // R2 does not support the AWS SDK's default streaming (chunked) SigV4
            // payload signing; disable it and let the SDK sign the full payload.
            DisablePayloadSigning = true,
        };
        // Belt-and-braces: keep objects out of any cache and off private-by-default.
        request.Headers.CacheControl = "private, no-store";

        await _s3.PutObjectAsync(request, ct);
        return key;
    }

    public async Task<Stream?> OpenReadAsync(string storageKey, CancellationToken ct = default)
    {
        try
        {
            var response = await _s3.GetObjectAsync(_bucket, storageKey, ct);
            return response.ResponseStream;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }
}
