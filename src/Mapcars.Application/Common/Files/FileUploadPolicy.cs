using Mapcars.Domain.Exceptions;

namespace Mapcars.Application.Common.Files;

/// <summary>
/// Central allowlist + size policy for every uploaded file (KYC documents,
/// vehicle photos, profile pictures). Enforced in the Application layer so it
/// cannot be bypassed by a caller. Security notes:
///   • allowlist by declared content-type AND file extension — deny by default;
///   • deliberately excludes <c>image/svg+xml</c> and <c>text/html</c> (both can
///     carry script and would be an XSS vector if ever served inline);
///   • hard size cap to blunt storage-exhaustion / DoS via huge uploads.
/// Declared content-type is client-supplied and therefore untrusted — callers
/// must additionally serve stored files with <c>X-Content-Type-Options: nosniff</c>
/// and never render them inline from a first-party origin.
/// </summary>
public static class FileUploadPolicy
{
    public const long MaxImageBytes = 20 * 1024 * 1024;     // 20 MB — photos
    public const long MaxDocumentBytes = 30 * 1024 * 1024;  // 30 MB — scanned PDFs

    private static readonly HashSet<string> ImageContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/png", "image/webp", "image/heic", "image/heif",
    };

    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp", ".heic", ".heif",
    };

    private static readonly HashSet<string> DocumentContentTypes =
        new(ImageContentTypes, StringComparer.OrdinalIgnoreCase) { "application/pdf" };

    private static readonly HashSet<string> DocumentExtensions =
        new(ImageExtensions, StringComparer.OrdinalIgnoreCase) { ".pdf" };

    /// <summary>Validates a profile/vehicle photo (image formats only).</summary>
    public static void EnsureValidImage(string contentType, string fileName, long byteLength)
        => Ensure(contentType, fileName, byteLength, ImageContentTypes, ImageExtensions, MaxImageBytes,
            "Upload a JPG, PNG, WEBP or HEIC image.");

    /// <summary>Validates a KYC/licensing document (image formats or PDF).</summary>
    public static void EnsureValidDocument(string contentType, string fileName, long byteLength)
        => Ensure(contentType, fileName, byteLength, DocumentContentTypes, DocumentExtensions, MaxDocumentBytes,
            "Upload a JPG, PNG, WEBP, HEIC image or a PDF.");

    /// <summary>
    /// Returns a safe, lower-cased extension for building a storage key, or an
    /// empty string. Never derive a storage key from the raw filename — that
    /// would let a caller influence the object key (path traversal / overwrite).
    /// </summary>
    public static string SafeExtension(string fileName)
    {
        var ext = Path.GetExtension(fileName);
        return !string.IsNullOrEmpty(ext) && DocumentExtensions.Contains(ext)
            ? ext.ToLowerInvariant()
            : string.Empty;
    }

    private static void Ensure(
        string contentType, string fileName, long byteLength,
        HashSet<string> allowedTypes, HashSet<string> allowedExtensions, long maxBytes, string hint)
    {
        if (byteLength <= 0)
            throw new DomainException("The uploaded file is empty.");
        if (byteLength > maxBytes)
            throw new DomainException($"File exceeds the {maxBytes / (1024 * 1024)} MB limit.");

        // Strip any ";charset=" parameter before matching the media type.
        var mediaType = (contentType ?? string.Empty).Split(';')[0].Trim();
        if (!allowedTypes.Contains(mediaType))
            throw new DomainException($"Unsupported file type. {hint}");

        var ext = Path.GetExtension(fileName ?? string.Empty);
        if (string.IsNullOrEmpty(ext) || !allowedExtensions.Contains(ext))
            throw new DomainException($"Unsupported file extension. {hint}");
    }
}
