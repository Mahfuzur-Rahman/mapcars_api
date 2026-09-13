namespace Mapcars.Domain.Constants;

/// <summary>
/// The user-type / role vocabulary, in one place.
///
/// <para>
/// These values are not merely compile-time symbols. <see cref="Customer"/> is
/// minted into every JWT (twice — as <c>user_type</c> and as the standard role
/// claim, see <c>JwtService</c>) and is <b>persisted as data</b> in six tables:
/// <c>ratings.rater_type</c>, <c>trip_messages.sender_type</c>,
/// <c>verification_codes.user_type</c>, <c>device_tokens.user_type</c>,
/// <c>refresh_tokens.user_type</c> and <c>error_logs.user_type</c> — three of
/// them behind CHECK constraints. Changing a value here without migrating that
/// data is a breaking change, not a rename.
/// </para>
///
/// <para>
/// <b>Why this file exists.</b> The Rider → Customer rename has to flip a value
/// that is simultaneously in flight inside live JWTs, sitting in six tables, and
/// compared against in roughly forty places. Centralising it first turns the
/// cutover into a one-line diff that can be read in full inside a maintenance
/// window, and makes the eventual removal of the legacy value greppable rather
/// than archaeological.
/// </para>
/// </summary>
public static class UserTypes
{
    /// <summary>
    /// The canonical passenger role. This line <b>is</b> the rename cutover: it
    /// was flipped from <c>"rider"</c> in the same commit as
    /// <c>031_rider_to_customer.sql</c>, and the two must be deployed together —
    /// the image and the database are one artifact from that point on.
    /// </summary>
    public const string Customer = "customer";

    /// <summary>
    /// What the passenger role was called before the rename.
    ///
    /// <para>
    /// Still accepted on the way IN — access tokens minted before the cutover
    /// carry it and stay valid for up to <c>Jwt:ExpiryMinutes</c>, and a client
    /// build older than the rename still sends it. Never emitted on the way out.
    /// </para>
    ///
    /// <para>
    /// Kept as its own symbol so every site tolerating the old value can be
    /// found with a single search and deleted together, once no image older
    /// than the cutover can be rolled back to (see <c>032</c>).
    /// </para>
    /// </summary>
    public const string LegacyCustomer = "rider";

    public const string Driver = "driver";
    public const string Admin = "admin";

    /// <summary>
    /// Role list for <c>[Authorize(Roles = ...)]</c>, which needs a compile-time
    /// constant and treats the comma list as OR.
    ///
    /// <para>
    /// Expands to <c>"customer,rider"</c>. The second value is what keeps the
    /// cutover invisible: access tokens minted before it carry the old role and
    /// remain valid for up to <c>Jwt:ExpiryMinutes</c>, so nobody is signed out
    /// by the deploy. Drop the legacy half with <c>032</c>.
    /// </para>
    /// </summary>
    public const string CustomerRoles = Customer + "," + LegacyCustomer;

    /// <summary>Both passenger spellings plus driver, for endpoints either may call.</summary>
    public const string CustomerOrDriverRoles = CustomerRoles + "," + Driver;

    /// <summary>
    /// Is this the passenger role, under either spelling?
    ///
    /// <para>
    /// Prefer this to a <c>switch</c> over <see cref="Customer"/> and
    /// <see cref="LegacyCustomer"/>. Before the cutover the two held the same
    /// string and duplicate case labels would not compile; they differ now, but
    /// routing every check through one predicate is still what makes the legacy
    /// value removable in one edit.
    /// </para>
    /// </summary>
    public static bool IsCustomer(string? userType) =>
        string.Equals(userType, Customer, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(userType, LegacyCustomer, StringComparison.OrdinalIgnoreCase);

    public static bool IsDriver(string? userType) =>
        string.Equals(userType, Driver, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Fold either passenger spelling onto the canonical one, so that code past
    /// this point only ever compares against a single value.
    ///
    /// <para>
    /// Call it <b>once, at the boundary</b> — where a claim is read off a token
    /// or a user type arrives on a request — not at each comparison. An unknown
    /// value is returned untouched rather than guessed at; callers decide what
    /// to do with it.
    /// </para>
    /// </summary>
    public static string Canonical(string? userType) =>
        IsCustomer(userType) ? Customer
        : IsDriver(userType) ? Driver
        : userType ?? string.Empty;
}
