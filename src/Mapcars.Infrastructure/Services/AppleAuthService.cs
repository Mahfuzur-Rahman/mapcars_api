using System.Security.Cryptography;
using System.Text;
using Mapcars.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Mapcars.Infrastructure.Services;

/// <summary>
/// Verifies Apple identity tokens. A singleton: it holds Apple's signing keys in
/// memory and one long-lived <see cref="HttpClient"/>.
///
/// <para><b>Audience</b> is what says the token was minted for <i>our</i> app —
/// skip it and any app's Apple token would sign its holder in here. It defaults
/// to the two iOS bundle ids, which are locked forever on the App Store (see
/// <c>Publish/TESTFLIGHT_RELEASE.md</c>); a web Services ID can be added with
/// <c>Apple:ClientIds</c> (env <c>Apple__ClientIds__0</c>) or a comma-separated
/// <c>Apple:ClientId</c>.</para>
///
/// <para><b>Keys rotate.</b> Apple publishes several at
/// <c>appleid.apple.com/auth/keys</c> and changes them without notice, so an
/// unknown <c>kid</c> triggers one refetch before a token is rejected — never
/// more than once a minute, so garbage tokens can't be used to hammer Apple.</para>
/// </summary>
public sealed class AppleAuthService : IAppleAuthService
{
    private const string Issuer = "https://appleid.apple.com";
    private const string KeysUrl = "https://appleid.apple.com/auth/keys";
    private static readonly string[] DefaultAudiences = ["com.mapcars.mapcars.user", "com.mapcars.mapcars.driver"];
    private static readonly TimeSpan KeyTtl = TimeSpan.FromHours(24);
    private static readonly TimeSpan MinRefetchInterval = TimeSpan.FromMinutes(1);

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(10) };
    private readonly SemaphoreSlim _refresh = new(1, 1);
    private readonly JsonWebTokenHandler _handler = new();
    private readonly IReadOnlyList<string> _audiences;
    private readonly ILogger<AppleAuthService> _logger;
    private readonly Func<CancellationToken, Task<string>> _fetchKeys;

    private IList<SecurityKey> _keys = [];
    private DateTime _fetchedAtUtc = DateTime.MinValue;

    public AppleAuthService(IConfiguration config, ILogger<AppleAuthService> logger)
        : this(config, logger, ct => Http.GetStringAsync(KeysUrl, ct)) { }

    /// <summary>Lets a test supply the JWKS instead of Apple.</summary>
    public AppleAuthService(
        IConfiguration config, ILogger<AppleAuthService> logger, Func<CancellationToken, Task<string>> fetchKeys)
    {
        _audiences = ReadAudiences(config);
        _logger = logger;
        _fetchKeys = fetchKeys;
    }

    public bool IsConfigured => _audiences.Count > 0;

    public async Task<AppleUserInfo?> VerifyIdTokenAsync(string idToken, string rawNonce, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(rawNonce))
        {
            _logger.LogWarning("Apple sign-in rejected: no nonce supplied.");
            return null;
        }

        try
        {
            var result = await ValidateAsync(idToken, forceRefresh: false, ct);
            if (result.Exception is SecurityTokenSignatureKeyNotFoundException)
                result = await ValidateAsync(idToken, forceRefresh: true, ct);

            if (!result.IsValid)
            {
                _logger.LogWarning("Apple token validation failed: {Message}", result.Exception?.Message);
                return null;
            }

            var claims = result.Claims;
            if (!claims.TryGetValue("nonce", out var nonce) || !FixedTimeEquals(nonce?.ToString(), Sha256Hex(rawNonce)))
            {
                _logger.LogWarning("Apple token rejected: nonce mismatch.");
                return null;
            }

            return new AppleUserInfo
            {
                Sub = claims.TryGetValue("sub", out var sub) ? sub?.ToString() ?? "" : "",
                Email = claims.TryGetValue("email", out var email) ? email?.ToString() : null,
                // Apple sends these as JSON booleans or as "true"/"false" strings.
                EmailVerified = IsTrue(claims, "email_verified"),
                IsPrivateEmail = IsTrue(claims, "is_private_email"),
            };
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogError(ex, "Couldn't fetch Apple's signing keys.");
            return null;
        }
    }

    private async Task<TokenValidationResult> ValidateAsync(string idToken, bool forceRefresh, CancellationToken ct)
    {
        var keys = await GetKeysAsync(forceRefresh, ct);
        return await _handler.ValidateTokenAsync(idToken, new TokenValidationParameters
        {
            ValidIssuer = Issuer,
            ValidAudiences = _audiences,
            IssuerSigningKeys = keys,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1),
            ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
        });
    }

    private async Task<IList<SecurityKey>> GetKeysAsync(bool forceRefresh, CancellationToken ct)
    {
        var age = DateTime.UtcNow - _fetchedAtUtc;
        if (_keys.Count > 0 && age < KeyTtl && (!forceRefresh || age < MinRefetchInterval))
            return _keys;

        await _refresh.WaitAsync(ct);
        try
        {
            age = DateTime.UtcNow - _fetchedAtUtc;
            if (_keys.Count > 0 && age < MinRefetchInterval) return _keys; // another caller just did it

            var json = await _fetchKeys(ct);
            _keys = new JsonWebKeySet(json).GetSigningKeys();
            _fetchedAtUtc = DateTime.UtcNow;
            return _keys;
        }
        finally
        {
            _refresh.Release();
        }
    }

    private static bool IsTrue(IDictionary<string, object> claims, string name)
        => claims.TryGetValue(name, out var v) && v switch
        {
            bool b => b,
            string s => bool.TryParse(s, out var parsed) && parsed,
            _ => false,
        };

    private static string Sha256Hex(string value)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private static bool FixedTimeEquals(string? a, string b)
        => a is not null && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(a), Encoding.UTF8.GetBytes(b));

    private static IReadOnlyList<string> ReadAudiences(IConfiguration config)
    {
        var ids = new List<string>(DefaultAudiences);

        var single = config["Apple:ClientId"];
        if (!string.IsNullOrWhiteSpace(single))
            ids.AddRange(single.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

        foreach (var child in config.GetSection("Apple:ClientIds").GetChildren())
            if (!string.IsNullOrWhiteSpace(child.Value)) ids.Add(child.Value.Trim());

        return ids.Distinct(StringComparer.Ordinal).ToList();
    }
}
