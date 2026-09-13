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
    /// The canonical passenger role. <b>Flipping this single line to
    /// <c>"customer"</c> is the rename cutover</b> — it must ship in the same
    /// breath as the migration that rewrites the stored values, never before
    /// and never after. See <c>031_rider_to_customer.sql</c>.
    /// </summary>
    public const string Customer = "rider";

    /// <summary>
    /// What the passenger role was called before the rename. Identical to
    /// <see cref="Customer"/> until the cutover, and deliberately kept as its
    /// own symbol so that every site tolerating the old value can be found with
    /// a single search and deleted together once no rollback target remains.
    /// </summary>
    public const string LegacyCustomer = "rider";

    public const string Driver = "driver";
    public const string Admin = "admin";

    /// <summary>
    /// Role list for <c>[Authorize(Roles = ...)]</c>, which needs a compile-time
    /// constant and treats the comma list as OR.
    ///
    /// <para>
    /// Before the cutover this expands to <c>"rider,rider"</c>, which is a
    /// harmless no-op — the framework splits and ORs, so a repeated value
    /// matches exactly as one would. After the cutover it becomes
    /// <c>"customer,rider"</c> and starts doing real work, accepting the
    /// access tokens already in flight (bounded by <c>Jwt:ExpiryMinutes</c>)
    /// without forcing anyone to sign in again.
    /// </para>
    /// </summary>
    public const string CustomerRoles = Customer + "," + LegacyCustomer;

    /// <summary>Both passenger spellings plus driver, for endpoints either may call.</summary>
    public const string CustomerOrDriverRoles = CustomerRoles + "," + Driver;

    /// <summary>
    /// Is this the passenger role, under either spelling?
    ///
    /// <para>
    /// Use this rather than a <c>switch</c> over <see cref="Customer"/> and
    /// <see cref="LegacyCustomer"/>: while the two constants hold the same
    /// value, duplicate case labels are a compile error.
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
