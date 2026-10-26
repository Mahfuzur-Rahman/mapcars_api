using Mapcars.Domain.Exceptions;

namespace Mapcars.Application.Common.Phones;

/// <summary>A reserved fake number that signs in with a fixed code, and is never texted.</summary>
public sealed record TestPhoneNumber(string Number, string Code);

/// <summary>
/// Which numbers may sign in by SMS, how many codes may go out per day, and the
/// reserved test numbers. Built from the "Sms" config section in
/// Infrastructure's DI. It validates itself on construction, so a bad config
/// stops the API at startup rather than at the first login.
/// </summary>
public sealed class PhoneLoginPolicy
{
    /// <summary>UK only, 500 a day, no test numbers. What tests and an unconfigured host get.</summary>
    public static PhoneLoginPolicy Default { get; } = new(["GB"], 500, []);

    private readonly Dictionary<string, string> _testCodes;

    public PhoneLoginPolicy(
        IReadOnlyCollection<string> allowedRegions,
        int dailySendCap,
        IReadOnlyCollection<TestPhoneNumber> testNumbers)
    {
        if (allowedRegions.Count == 0)
            throw new InvalidOperationException("Sms:AllowedRegions must name at least one region (e.g. GB).");
        if (dailySendCap <= 0)
            throw new InvalidOperationException("Sms:DailySendCap must be greater than zero.");

        foreach (var t in testNumbers)
        {
            if (!PhoneFormat.IsReservedTestNumber(t.Number))
                throw new InvalidOperationException(
                    $"Sms:TestNumbers: {t.Number} is outside Ofcom's reserved range +447700900000 to +447700900999. " +
                    "Refusing to start: a real number here would be a fixed-code backdoor into that person's account.");
            if (t.Code is not { Length: 6 } || !t.Code.All(char.IsAsciiDigit))
                throw new InvalidOperationException($"Sms:TestNumbers: the code for {t.Number} must be exactly 6 digits.");
        }

        AllowedRegions = allowedRegions.Select(r => r.Trim().ToUpperInvariant()).ToHashSet();
        DailySendCap = dailySendCap;
        _testCodes = testNumbers.ToDictionary(t => t.Number, t => t.Code);
    }

    public IReadOnlySet<string> AllowedRegions { get; }

    /// <summary>Real SMS allowed per rolling 24 h, platform-wide: a cost circuit-breaker.</summary>
    public int DailySendCap { get; }

    /// <summary>The fixed code for a configured test number, or null for any other number.</summary>
    public string? TestCodeFor(string e164) => _testCodes.GetValueOrDefault(e164);

    /// <summary>
    /// The E.164 form of a sign-in phone number. Throws
    /// <see cref="DomainException"/> (a 400 the clients show as-is) for anything
    /// that isn't a mobile in an allowed region. Test numbers are accepted before
    /// validation, because the reserved range needn't be "valid" in libphonenumber's data.
    /// </summary>
    public string NormalizeLoginPhone(string raw)
    {
        var candidate = PhoneFormat.Clean(raw);
        if (_testCodes.ContainsKey(candidate)) return candidate;

        return PhoneFormat.TryNormalizeMobile(candidate, AllowedRegions, out var e164)
            ? e164
            : throw new DomainException("Enter a valid UK mobile number.");
    }
}
