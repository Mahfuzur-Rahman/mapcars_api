namespace Mapcars.Application.Admins;

/// <summary>
/// <c>menus.path</c> values that admin endpoints are gated on with
/// <c>[RequiresAdminMenu]</c>. They are data — seeded by <c>database/*.sql</c>
/// (001, 016, 020, 021, 022, 027, 031, 034, 042) — so they are spelled once here
/// rather than at each endpoint: a typo would not fail anything, it would silently
/// lock every non-SuperAdmin out of that endpoint. Change a menu's path in SQL and
/// you must change it here in the same deploy.
/// <para>
/// Only the menus some endpoint is gated on are listed — Dashboard (<c>/admin</c>)
/// is not: it is the landing page every admin is sent to. The last group backs
/// SuperAdmin-only endpoints; a menu only ever narrows the role check, so there
/// it changes nothing for an active SuperAdmin and refuses a disabled one.
/// </para>
/// </summary>
public static class AdminMenuPaths
{
    public const string CustomerList = "/admin/customers";
    public const string CustomerDetail = "/admin/customers/[id]";

    public const string DriverList = "/admin/drivers";
    public const string DriverDetail = "/admin/drivers/[id]";
    public const string DriverDocuments = "/admin/drivers/documents";
    public const string TierAppeals = "/admin/tier-appeals";

    public const string LiveMap = "/admin/trips/live";
    public const string TripHistory = "/admin/trips/history";

    public const string Transactions = "/admin/payments/transactions";
    public const string Payouts = "/admin/payments/payouts";
    public const string Statements = "/admin/payments/statements";

    public const string Posters = "/admin/posters";
    public const string ErrorLogs = "/admin/error-logs";
    public const string Emails = "/admin/emails";

    // Behind SuperAdmin-only endpoints (see above).
    public const string AdminUsers = "/admin/admins";
    public const string FareSettings = "/admin/fare";
    public const string PaymentSettings = "/admin/settings/payments";
}
