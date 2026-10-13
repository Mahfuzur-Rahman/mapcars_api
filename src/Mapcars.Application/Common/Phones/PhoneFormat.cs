using PhoneNumbers;

namespace Mapcars.Application.Common.Phones;

/// <summary>
/// Turns what people type into one canonical phone format (E.164, "+447400123456").
/// Pure and stateless. The decision about WHICH numbers may sign in lives in
/// <see cref="PhoneLoginPolicy"/>.
/// </summary>
public static class PhoneFormat
{
    private static readonly PhoneNumberUtil Util = PhoneNumberUtil.GetInstance();

    /// <summary>
    /// The best E.164 candidate for a UK-centric input, before validation. It
    /// keeps digits and a leading '+', and repairs the forms our own clients and
    /// people produce:
    /// <list type="bullet">
    ///   <item>"+440…" or "440…" (+44 glued to a trunk 0, what the apps sent) becomes "+44…";</item>
    ///   <item>"44" plus 10 digits gets a "+";</item>
    ///   <item>"0" plus 10 digits (a national number) becomes "+44" plus 10.</item>
    /// </list>
    /// Anything else comes back as digits, for libphonenumber to judge.
    /// </summary>
    public static string Clean(string raw)
    {
        var kept = new string(raw.Where(c => char.IsAsciiDigit(c) || c == '+').ToArray());
        if (kept.Length == 0) return kept;

        var plus = kept[0] == '+';
        var digits = kept.Replace("+", "");

        if (digits.StartsWith("440")) return "+44" + digits[3..];
        if (plus) return "+" + digits;
        if (digits.StartsWith("44") && digits.Length == 12) return "+" + digits;
        if (digits.StartsWith('0') && digits.Length == 11) return "+44" + digits[1..];
        return digits;
    }

    /// <summary>
    /// True, with the E.164 form, when <paramref name="candidate"/> parses as a
    /// valid MOBILE number in one of <paramref name="allowedRegions"/>. A number
    /// without a country code is read as GB.
    /// </summary>
    public static bool TryNormalizeMobile(string candidate, IReadOnlySet<string> allowedRegions, out string e164)
    {
        e164 = string.Empty;
        try
        {
            var number = Util.Parse(candidate, "GB");
            if (!Util.IsValidNumber(number)) return false;

            var type = Util.GetNumberType(number);
            if (type is not (PhoneNumberType.MOBILE or PhoneNumberType.FIXED_LINE_OR_MOBILE)) return false;

            var region = Util.GetRegionCodeForNumber(number);
            if (region is null || !allowedRegions.Contains(region)) return false;

            e164 = Util.Format(number, PhoneNumberFormat.E164);
            return true;
        }
        catch (NumberParseException)
        {
            return false;
        }
    }

    /// <summary>
    /// For numbers that are stored but never used to sign in (emergency contacts,
    /// admin-entered phones). E.164 when the input parses as a valid number in
    /// any country, and the trimmed input unchanged otherwise. A contact number
    /// is the user's to get wrong, and refusing to save it would be worse.
    /// </summary>
    public static string NormalizeAny(string raw)
    {
        try
        {
            var number = Util.Parse(Clean(raw), "GB");
            if (Util.IsValidNumber(number)) return Util.Format(number, PhoneNumberFormat.E164);
        }
        catch (NumberParseException)
        {
            // fall through
        }
        return raw.Trim();
    }

    /// <summary>
    /// Ofcom's reserved drama range, 07700 900000 to 07700 900999. No real
    /// subscriber can hold these numbers, which is what makes them safe as
    /// fixed-code test logins.
    /// </summary>
    public static bool IsReservedTestNumber(string e164)
        => e164.Length == 13 && e164.StartsWith("+447700900", StringComparison.Ordinal)
           && e164[10..].All(char.IsAsciiDigit);
}
