using Mapcars.Domain.Entities;
using Mapcars.Domain.Enums;

namespace Mapcars.Application.Drivers;

/// <summary>
/// The single rule for "may this driver work?". A driver may only go online,
/// see the requests board, push their live location and accept trips once an
/// admin has reviewed their documents and set them to <see cref="DriverStatus.Approved"/>.
/// Nothing — not the environment, not a demo account, not a client flag —
/// bypasses this; approval is only ever granted by an explicit admin decision
/// (<c>AdminDriverReviewController</c> → <c>SetDriverStatus</c>).
/// </summary>
public static class DriverApproval
{
    public static bool CanWork(Driver driver) => driver.Status == DriverStatus.Approved;

    /// <summary>Why this driver may not work yet, in words a driver can act on.</summary>
    public static string BlockedMessage(DriverStatus status) => status switch
    {
        DriverStatus.Suspended => "Your account is suspended. Contact Mapcars support before you can go online again.",
        DriverStatus.Rejected => "Your application was not approved, so you can't go online. Contact Mapcars support.",
        _ => "Your account is awaiting approval. An admin must verify your documents before you can go online and receive trip requests.",
    };
}
