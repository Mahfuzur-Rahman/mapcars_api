using System.Net;
using Mapcars.Application.Trips.Emails;
using Mapcars.Domain.Entities;
using Mapcars.Domain.Enums;

namespace Mapcars.Application.Tests;

/// <summary>
/// The receipt and the driver's summary are HTML built from customer- and
/// driver-chosen text (names, typed addresses), and they state money. Both must
/// be right: encoded, and penny-consistent with what the app shows.
/// </summary>
public class TripCompletionEmailsTests
{
    private const string Hostile = "<img src=x onerror=alert(1)>";

    private static Trip CompletedTrip(PaymentMethod method = PaymentMethod.Cash, PaymentStatus status = PaymentStatus.Collected) => new()
    {
        PickupAddress = "1 High St, Poole",
        DropoffAddress = "Bournemouth Station",
        Status = TripStatus.Completed,
        CompletedAtUtc = new DateTime(2026, 7, 1, 13, 5, 0, DateTimeKind.Utc), // 14:05 BST
        Tier = "xl",
        DistanceMiles = 4.24,
        FareAmount = 11.00m,
        TipAmount = 1.50m,
        PlatformFeeAmount = 1.65m,
        DriverEarnings = 10.85m,
        PaymentMethod = method,
        PaymentStatus = status,
    };

    private static Driver NewDriver(string? first = "Sam") => new() { FirstName = first, FullName = $"{first} Taylor", Email = "d@example.com" };
    /// <summary>What the reader sees: the encoder writes "£" as "&amp;#163;".</summary>
    private static string Text(string html) => WebUtility.HtmlDecode(html);

    private static Customer NewCustomer(string name = "Alex Jones") => new() { FullName = name, Email = "c@example.com" };

    [Fact]
    public void Customer_receipt_totals_fare_and_tip_and_shows_the_trip()
    {
        var vehicle = new Vehicle { Make = "Toyota", Model = "Prius", Colour = "Silver", RegistrationNumber = "AB12 CDE" };

        var trip = CompletedTrip();

        var (subject, html) = TripCompletionEmails.ForCustomer(trip, NewCustomer(), NewDriver(), vehicle);
        var body = Text(html);

        Assert.Equal("Your MapCars receipt — £12.50", subject);
        Assert.Contains("Thanks for riding with MapCars, Alex.", body);
        Assert.Contains("XL fare", body);
        Assert.Contains("£11.00", body);
        Assert.Contains("£1.50", body);
        Assert.Contains("Paid in cash to your driver", body);
        Assert.Contains("Bournemouth Station", body);
        Assert.Contains("4.2 miles", body);
        Assert.Contains("Wed 1 Jul 2026, 14:05", body); // UK local time, not UTC
        Assert.Contains("Silver Toyota Prius · AB12 CDE", body);
        Assert.Contains(TripCompletionEmails.ReceiptNumber(trip), body);
    }

    [Fact]
    public void Driver_summary_itemises_fee_and_tip_down_to_the_stored_earnings()
    {
        var (subject, html) = TripCompletionEmails.ForDriver(CompletedTrip(), NewDriver(), NewCustomer());
        var body = Text(html);

        Assert.Equal("Trip complete — you earned £10.85", subject);
        Assert.Contains("Nice work, Sam.", body);
        Assert.Contains("your trip with Alex.", body);
        Assert.DoesNotContain("Jones", body); // first name only
        Assert.Contains("−£1.65", body);
        Assert.Contains("+£1.50", body);
        Assert.Contains("you collected £12.50 from the customer", body);
    }

    [Fact]
    public void A_driver_never_learns_the_customers_card_state()
    {
        var body = Text(TripCompletionEmails.ForDriver(
            CompletedTrip(PaymentMethod.Card, PaymentStatus.Failed), NewDriver(), NewCustomer()).Body);

        Assert.Contains("Paid by card", body);
        Assert.DoesNotContain("couldn't", body);
        Assert.DoesNotContain("Failed", body);
    }

    [Fact]
    public void An_unsettled_card_trip_is_not_called_charged()
    {
        var body = Text(TripCompletionEmails.ForCustomer(
            CompletedTrip(PaymentMethod.Card, PaymentStatus.Pending), NewCustomer(), NewDriver(), null).Body);

        Assert.Contains("£12.50 will be charged to your card", body);
        Assert.DoesNotContain("Charged to your card", body);
    }

    [Fact]
    public void Names_and_typed_addresses_are_encoded_in_both_emails()
    {
        var trip = CompletedTrip();
        trip.PickupAddress = Hostile;
        var driver = NewDriver(Hostile);
        var customer = NewCustomer(Hostile);
        var vehicle = new Vehicle { Make = Hostile, Model = "X", Colour = "Red", RegistrationNumber = "R1" };

        var (_, toCustomer) = TripCompletionEmails.ForCustomer(trip, customer, driver, vehicle);
        var (_, toDriver) = TripCompletionEmails.ForDriver(trip, driver, customer);

        foreach (var body in new[] { toCustomer, toDriver })
        {
            Assert.DoesNotContain("<img", body);
            Assert.Contains("&lt;img", body);
        }
    }

    [Theory]
    [InlineData("c@example.com", true, "c@example.com")]
    [InlineData("c@example.com", false, null)] // phone sign-up that never verified the email it added
    [InlineData(null, true, null)]
    [InlineData("  ", true, null)]
    public void Only_a_verified_email_gets_the_trip_email(string? email, bool verified, string? expected)
    {
        Assert.Equal(expected, TripCompletionEmails.AddressFor(new Customer { Email = email, IsEmailVerified = verified }));
        Assert.Equal(expected, TripCompletionEmails.AddressFor(new Driver { Email = email, IsEmailVerified = verified }));
    }
}
