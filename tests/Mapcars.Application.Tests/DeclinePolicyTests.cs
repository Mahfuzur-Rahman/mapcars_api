using Mapcars.Application.Payments;

namespace Mapcars.Application.Tests;

/// <summary>
/// What to do about a declined card.
///
/// Getting this wrong is not a crash: it is either chasing a card that will
/// never work, or giving up on one that would have paid on the second try.
/// </summary>
public class DeclinePolicyTests
{
    [Theory]
    [InlineData("insufficient_funds")]
    [InlineData("generic_decline")]
    [InlineData("try_again_later")]
    [InlineData("processing_error")]
    [InlineData("issuer_not_available")]
    public void Transient_declines_are_retryable(string code)
        => Assert.True(DeclinePolicy.IsRetryable(code));

    [Theory]
    [InlineData("expired_card")]
    [InlineData("incorrect_cvc")]
    [InlineData("incorrect_number")]
    [InlineData("invalid_account")]
    [InlineData("card_not_supported")]
    [InlineData("currency_not_supported")]
    [InlineData("lost_card")]
    [InlineData("stolen_card")]
    [InlineData("pickup_card")]
    public void Permanent_declines_are_not_retryable_and_retire_the_card(string code)
    {
        Assert.False(DeclinePolicy.IsRetryable(code));
        Assert.True(DeclinePolicy.ShouldDeactivateCard(code));
        Assert.Null(DeclinePolicy.NextRetryAtUtc(code, 0, DateTime.UtcNow));
    }

    [Fact]
    public void A_transient_decline_never_retires_the_card()
    {
        // A card that merely had no money in it on Tuesday is still a good card.
        Assert.False(DeclinePolicy.ShouldDeactivateCard("insufficient_funds"));
    }

    [Fact]
    public void Authentication_required_is_not_a_decline_and_is_never_retried_unattended()
    {
        // Retrying this off-session fails identically, forever. Only an
        // on-session challenge clears it — so there must be no scheduled retry.
        Assert.Equal(DeclineAction.RequiresAuthentication, DeclinePolicy.ActionFor("authentication_required"));
        Assert.False(DeclinePolicy.IsRetryable("authentication_required"));
        Assert.Null(DeclinePolicy.NextRetryAtUtc("authentication_required", 0, DateTime.UtcNow));
        Assert.False(DeclinePolicy.ShouldDeactivateCard("authentication_required"));
    }

    [Fact]
    public void An_unknown_code_is_retried_rather_than_stranding_the_fare()
    {
        // A code we have never seen is more likely transient than permanent, and
        // the attempt cap stops this becoming an infinite loop.
        Assert.True(DeclinePolicy.IsRetryable("some_future_code"));
        Assert.False(DeclinePolicy.ShouldDeactivateCard("some_future_code"));
    }

    [Fact]
    public void Null_and_empty_codes_do_not_throw()
    {
        Assert.True(DeclinePolicy.IsRetryable(null));
        Assert.True(DeclinePolicy.IsRetryable(""));
    }

    [Fact]
    public void Decline_codes_are_matched_case_insensitively()
        => Assert.Equal(DeclineAction.RequiresAuthentication, DeclinePolicy.ActionFor("Authentication_Required"));

    [Fact]
    public void The_retry_schedule_is_fifteen_minutes_then_two_hours_then_a_day()
    {
        var now = new DateTime(2026, 9, 14, 12, 0, 0, DateTimeKind.Utc);
        Assert.Equal(now.AddMinutes(15), DeclinePolicy.NextRetryAtUtc("insufficient_funds", 0, now));
        Assert.Equal(now.AddHours(2), DeclinePolicy.NextRetryAtUtc("insufficient_funds", 1, now));
        Assert.Equal(now.AddHours(24), DeclinePolicy.NextRetryAtUtc("insufficient_funds", 2, now));
    }

    [Fact]
    public void Automatic_retries_stop_at_the_cap()
    {
        var now = DateTime.UtcNow;
        Assert.Null(DeclinePolicy.NextRetryAtUtc("insufficient_funds", DeclinePolicy.MaxAutoRetryAttempts, now));
        Assert.Null(DeclinePolicy.NextRetryAtUtc("insufficient_funds", 99, now));
    }

    [Theory]
    [InlineData("card_velocity_exceeded")]
    [InlineData("do_not_honor")]
    public void A_velocity_decline_waits_for_the_issuers_day_to_roll_over(string code)
    {
        var now = new DateTime(2026, 9, 14, 12, 0, 0, DateTimeKind.Utc);

        // Retrying in fifteen minutes is pointless when the issuer has hit a
        // limit; the only attempt with a chance is after the day rolls over.
        Assert.Equal(now.AddHours(24), DeclinePolicy.NextRetryAtUtc(code, 0, now));

        // And only that one attempt.
        Assert.Null(DeclinePolicy.NextRetryAtUtc(code, 1, now));
    }

    [Fact]
    public void A_customer_is_never_told_their_card_was_reported_lost_or_stolen()
    {
        // The person holding the phone may not be the person who reported it.
        // Providers are explicit about this, so the wording stays generic.
        foreach (var code in new[] { "lost_card", "stolen_card", "pickup_card" })
        {
            var message = DeclinePolicy.CustomerMessageFor(code);
            Assert.DoesNotContain("lost", message, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("stolen", message, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("report", message, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void Customer_messages_are_never_empty_and_never_leak_the_raw_code()
    {
        foreach (var code in new[]
                 {
                     "insufficient_funds", "expired_card", "incorrect_cvc",
                     "authentication_required", "card_velocity_exceeded",
                     "some_future_code", "",
                 })
        {
            var message = DeclinePolicy.CustomerMessageFor(code);
            Assert.False(string.IsNullOrWhiteSpace(message));
            Assert.DoesNotContain("_", message);
        }
    }
}
