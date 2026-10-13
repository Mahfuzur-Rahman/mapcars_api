using System.Globalization;
using Mapcars.Application.Common;
using Mapcars.Application.Trips.Mapping;
using Mapcars.Domain.Entities;
using Mapcars.Domain.Enums;
using H = Mapcars.Application.Common.EmailHtml;

namespace Mapcars.Application.Trips.Emails;

/// <summary>
/// The two emails a completed trip produces: the customer's receipt and the
/// driver's earnings summary. Pure — no I/O — so the wording, the money and the
/// encoding of every name and address can be tested without a mail provider.
///
/// <para>
/// Each party only sees what the app already shows them: the driver gets the
/// customer's first name and never their card state (<see cref="DriverPaymentView"/>),
/// and the customer gets the driver's first name, car and plate.
/// </para>
/// </summary>
public static class TripCompletionEmails
{
    private static readonly TimeZoneInfo UkZone = TimeZoneInfo.FindSystemTimeZoneById("Europe/London");
    private static readonly CultureInfo UkCulture = CultureInfo.GetCultureInfo("en-GB");

    public static (string Subject, string Body) ForCustomer(Trip trip, Customer customer, Driver? driver, Vehicle? vehicle)
    {
        var farePence = Money.ToPence(trip.FareAmount ?? 0m);
        var tipPence = Money.ToPence(trip.TipAmount);
        var totalPence = Money.SumToPence(trip.FareAmount ?? 0m, trip.TipAmount);
        var total = Money.Format(totalPence);

        var greeting = FirstName(customer.FullName) is { } first
            ? $"Thanks for riding with MapCars, {H.Encode(first)}."
            : "Thanks for riding with MapCars.";

        var fare = new List<(string, string)> { ($"{TierName(trip.Tier)} fare", Money.Format(farePence)) };
        if (tipPence > 0) fare.Add(("Tip for your driver", Money.Format(tipPence)));
        fare.Add(("Payment", trip.PaymentMethod == PaymentMethod.Cash ? "Cash" : "Card"));

        var facts = new List<(string, string)>();
        if (trip.CompletedAtUtc is { } done) facts.Add(("Completed", UkTime(done)));
        if (trip.DistanceMiles is { } miles) facts.Add(("Distance", Miles(miles)));
        if (driver is not null)
            facts.Add(("Driver", FirstName(driver.FirstName) ?? FirstName(driver.FullName) ?? "Your driver"));
        if (vehicle is not null)
            facts.Add(("Vehicle", $"{vehicle.Colour} {vehicle.Make} {vehicle.Model} · {vehicle.RegistrationNumber}"));
        facts.Add(("Receipt no.", ReceiptNumber(trip)));

        var body =
            H.Paragraph(greeting + " Here's your receipt.") +
            H.Amount("Total", total, CustomerPaymentCaption(trip, total)) +
            H.Heading("Your trip") +
            H.Route(trip.PickupAddress, trip.DropoffAddress) +
            H.Heading("Fare") +
            H.Details(fare, ("Total", total)) +
            H.Heading("Details") +
            H.Details(facts) +
            H.Muted(
                $"Left something behind or have a question about this trip? Email " +
                $"<a href=\"mailto:{EmailBrand.SupportEmail}\" style=\"color:{EmailBrand.BrandDark}\">{EmailBrand.SupportEmail}</a> " +
                $"and quote {ReceiptNumber(trip)}.");

        return ($"Your MapCars receipt — {total}", body);
    }

    public static (string Subject, string Body) ForDriver(Trip trip, Driver driver, Customer? customer)
    {
        var farePence = Money.ToPence(trip.FareAmount ?? 0m);
        var feePence = Money.ToPence(trip.PlatformFeeAmount ?? 0m);
        var tipPence = Money.ToPence(trip.TipAmount);
        // The stored figure is the one statements are built from; derive it only
        // for a trip priced before the snapshot existed.
        var earningsPence = trip.DriverEarnings is { } stored
            ? Money.ToPence(stored)
            : farePence - feePence + tipPence;
        var earnings = Money.Format(earningsPence);

        var name = FirstName(driver.FirstName) ?? FirstName(driver.FullName);
        var rider = FirstName(customer?.FullName);
        var intro = (name is null ? "Nice work." : $"Nice work, {H.Encode(name)}.") +
                    (rider is null ? " Here's the summary of your trip." : $" Here's the summary of your trip with {H.Encode(rider)}.");

        var breakdown = new List<(string, string)> { ($"{TierName(trip.Tier)} fare", Money.Format(farePence)) };
        if (feePence > 0) breakdown.Add(("MapCars fee", "−" + Money.Format(feePence)));
        if (tipPence > 0) breakdown.Add(("Tip", "+" + Money.Format(tipPence)));

        var facts = new List<(string, string)>();
        if (trip.CompletedAtUtc is { } done) facts.Add(("Completed", UkTime(done)));
        if (trip.DistanceMiles is { } miles) facts.Add(("Distance", Miles(miles)));
        facts.Add(("Trip ref.", ReceiptNumber(trip)));

        var collected = Money.Format(Money.SumToPence(trip.FareAmount ?? 0m, trip.TipAmount));
        var payment = trip.PaymentMethod == PaymentMethod.Cash
            ? $"Paid in cash — you collected {collected} from the customer. The MapCars fee for this trip appears on your weekly statement."
            : "Paid by card — MapCars handles the customer's payment. This trip appears on your weekly statement.";

        var body =
            H.Paragraph(intro) +
            H.Amount("You earned", earnings, payment) +
            H.Heading("Trip") +
            H.Route(trip.PickupAddress, trip.DropoffAddress) +
            H.Heading("Earnings") +
            H.Details(breakdown, ("Your earnings", earnings)) +
            H.Heading("Details") +
            H.Details(facts) +
            H.Muted("Thanks for driving with MapCars. Your weekly statement in the app has the full breakdown.");

        return ($"Trip complete — you earned {earnings}", body);
    }

    /// <summary>
    /// Where a party's trip email goes, or null for none. Only a verified address:
    /// email sign-ups verify at registration, phone sign-ups verify the email they
    /// add on their profile. An unverified one may be a typo, and the email
    /// carries pickup and drop-off addresses.
    /// </summary>
    public static string? AddressFor(Customer? customer) =>
        customer is { IsEmailVerified: true, Email: { } email } && !string.IsNullOrWhiteSpace(email) ? email : null;

    /// <inheritdoc cref="AddressFor(Customer?)"/>
    public static string? AddressFor(Driver? driver) =>
        driver is { IsEmailVerified: true, Email: { } email } && !string.IsNullOrWhiteSpace(email) ? email : null;

    /// <summary>Same number the in-app receipt shows (<c>GetReceiptForUserAsync</c>).</summary>
    public static string ReceiptNumber(Trip trip) => $"MC-{trip.Id.ToString()[..8].ToUpperInvariant()}";

    /// <summary>
    /// The customer's real settlement state. The card charge runs after drop-off,
    /// so at completion a card trip is usually not settled yet.
    /// </summary>
    private static string CustomerPaymentCaption(Trip trip, string total)
    {
        if (trip.PaymentMethod == PaymentMethod.Cash) return "Paid in cash to your driver";
        return trip.PaymentStatus switch
        {
            PaymentStatus.Collected => "Charged to your card",
            PaymentStatus.Refunded => "Refunded to your card",
            PaymentStatus.Failed or PaymentStatus.ActionRequired =>
                "We couldn't take this card payment yet — open the app to complete it",
            _ => $"{total} will be charged to your card",
        };
    }

    private static string TierName(string? tier) => tier?.ToLowerInvariant() switch
    {
        null or "" => "Ride",
        "xl" => "XL",
        var t => char.ToUpperInvariant(t[0]) + t[1..],
    };

    private static string? FirstName(string? name)
    {
        var first = name?.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        return string.IsNullOrEmpty(first) ? null : first;
    }

    private static string Miles(double miles) => $"{miles.ToString("0.0", UkCulture)} miles";

    private static string UkTime(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), UkZone)
            .ToString("ddd d MMM yyyy, HH:mm", UkCulture);
}
