namespace Mapcars.Infrastructure.Options;

/// <summary>
/// Cloudflare R2 (S3-compatible) storage credentials. Bound from the
/// "Storage:R2" config section. All values are secrets — they live only in the
/// gitignored appsettings.json (mirrored to keys/) and are never logged.
/// </summary>
public class R2Options
{
    public const string Section = "Storage:R2";

    /// <summary>Cloudflare account id — forms the endpoint host.</summary>
    public string AccountId { get; init; } = string.Empty;

    public string AccessKeyId { get; init; } = string.Empty;
    public string SecretAccessKey { get; init; } = string.Empty;

    /// <summary>The (private) bucket documents are stored in.</summary>
    public string BucketName { get; init; } = string.Empty;

    /// <summary>
    /// Full S3 endpoint override. Set this for jurisdiction-scoped buckets, whose
    /// host differs from the default — e.g. EU: <c>https://&lt;account&gt;.eu.r2.cloudflarestorage.com</c>.
    /// When empty, the default <c>https://&lt;account&gt;.r2.cloudflarestorage.com</c> is used.
    /// </summary>
    public string EndpointUrl { get; init; } = string.Empty;

    /// <summary>The resolved S3 service URL (override if set, else the default host).</summary>
    public string ResolveServiceUrl() => string.IsNullOrWhiteSpace(EndpointUrl)
        ? $"https://{AccountId}.r2.cloudflarestorage.com"
        : EndpointUrl;
}
