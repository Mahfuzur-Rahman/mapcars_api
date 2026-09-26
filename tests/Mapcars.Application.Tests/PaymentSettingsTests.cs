using System.Text.Json;
using FluentValidation.TestHelper;
using Mapcars.Application.Settings.Dtos;
using Mapcars.Application.Settings.Models;
using Mapcars.Application.Settings.Validators;

namespace Mapcars.Application.Tests;

/// <summary>
/// The settings document is the one piece of payment behaviour that changes
/// without a deploy, and the one an admin can get wrong at 2am. These tests pin
/// the two properties that matter: it fails CLOSED when it cannot be read, and
/// an older document (written before a setting existed) upgrades to the safe
/// value rather than to zero.
/// </summary>
public class PaymentSettingsDefaultsTests
{
    [Fact]
    public void FreshDocument_TakesCashAndNotCard()
    {
        var s = new PaymentSettings();

        Assert.True(s.CashEnabled);
        Assert.False(s.CardEnabled);
        Assert.Equal(PaymentMethodNames.Cash, s.DefaultMethod);
    }

    [Fact]
    public void FreshDocument_HasEveryProtectionOn()
    {
        var s = new PaymentSettings();

        Assert.True(s.ChallengeOnNewDevice);
        Assert.True(s.ChallengeAfterFailedCharge);
        Assert.True(s.ChallengeUnauthenticatedCards);
    }

    [Fact]
    public void FreshDocument_HasFiniteLimits()
    {
        var s = new PaymentSettings();

        // A zero here would not mean "unlimited" to a reader of this class, but
        // it is exactly what an int lands on if a default is ever dropped - and
        // a zero card limit locks every customer out of paying.
        Assert.True(s.MaxSavedCardsPerCustomer > 0);
        Assert.True(s.MaxCardAddAttemptsPerDay > 0);
    }
}

/// <summary>
/// The store keeps this document as JSONB and deserialises with
/// <see cref="JsonSerializerDefaults.Web"/>. That is what makes "add a setting,
/// no migration" true - but only if a document missing the new key lands on the
/// property default instead of default(T).
/// </summary>
public class PaymentSettingsJsonTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>Exactly what the 033 migration seeds, and what every row written
    /// before the fraud settings existed still looks like.</summary>
    private const string LegacyDocument =
        """{"cashEnabled":true,"cardEnabled":false,"defaultMethod":"Cash"}""";

    [Fact]
    public void LegacyDocument_GainsTheNewSettingsAtTheirSafeDefaults()
    {
        var s = JsonSerializer.Deserialize<PaymentSettings>(LegacyDocument, Json)!;

        Assert.True(s.ChallengeOnNewDevice);
        Assert.True(s.ChallengeAfterFailedCharge);
        Assert.True(s.ChallengeUnauthenticatedCards);
        Assert.Equal(2500, s.ChallengeAboveFarePence);
        Assert.Equal(5, s.MaxSavedCardsPerCustomer);
        Assert.Equal(5, s.MaxCardAddAttemptsPerDay);
    }

    [Fact]
    public void LegacyDocument_KeepsTheMethodsItActuallyStated()
    {
        var s = JsonSerializer.Deserialize<PaymentSettings>(LegacyDocument, Json)!;

        Assert.True(s.CashEnabled);
        Assert.False(s.CardEnabled);
    }

    [Fact]
    public void AnExplicitFalse_IsNotOverwrittenByTheDefault()
    {
        // The mirror of the test above, and the one that would catch someone
        // "fixing" the missing-key case by forcing the protections on at read.
        var s = JsonSerializer.Deserialize<PaymentSettings>(
            """{"cashEnabled":true,"cardEnabled":true,"defaultMethod":"Card","challengeOnNewDevice":false}""",
            Json)!;

        Assert.False(s.ChallengeOnNewDevice);
        Assert.True(s.ChallengeAfterFailedCharge);
    }

    [Fact]
    public void RoundTrip_PreservesEverySetting()
    {
        var original = new PaymentSettings
        {
            CashEnabled = false,
            CardEnabled = true,
            DefaultMethod = PaymentMethodNames.Card,
            ChallengeOnNewDevice = false,
            ChallengeAfterFailedCharge = false,
            ChallengeUnauthenticatedCards = false,
            ChallengeAboveFarePence = 999,
            ReverifyAfterDormantDays = 180,
            MaxSavedCardsPerCustomer = 2,
            MaxCardAddAttemptsPerDay = 3,
            BlockBookingWhenDebtExceedsPence = 1500,
        };

        var back = JsonSerializer.Deserialize<PaymentSettings>(
            JsonSerializer.Serialize(original, Json), Json)!;

        Assert.Equal(original.CashEnabled, back.CashEnabled);
        Assert.Equal(original.CardEnabled, back.CardEnabled);
        Assert.Equal(original.DefaultMethod, back.DefaultMethod);
        Assert.Equal(original.ChallengeOnNewDevice, back.ChallengeOnNewDevice);
        Assert.Equal(original.ChallengeAfterFailedCharge, back.ChallengeAfterFailedCharge);
        Assert.Equal(original.ChallengeUnauthenticatedCards, back.ChallengeUnauthenticatedCards);
        Assert.Equal(original.ChallengeAboveFarePence, back.ChallengeAboveFarePence);
        Assert.Equal(original.ReverifyAfterDormantDays, back.ReverifyAfterDormantDays);
        Assert.Equal(original.MaxSavedCardsPerCustomer, back.MaxSavedCardsPerCustomer);
        Assert.Equal(original.MaxCardAddAttemptsPerDay, back.MaxCardAddAttemptsPerDay);
        Assert.Equal(original.BlockBookingWhenDebtExceedsPence, back.BlockBookingWhenDebtExceedsPence);
    }

    [Fact]
    public void UnknownKeys_DoNotBlowUp()
    {
        // Forward compatibility in the other direction: an older image reading a
        // document a newer one wrote. Throwing here would take payment settings
        // down platform-wide during a rollback.
        var s = JsonSerializer.Deserialize<PaymentSettings>(
            """{"cashEnabled":true,"cardEnabled":false,"defaultMethod":"Cash","somethingFromTheFuture":42}""",
            Json)!;

        Assert.True(s.CashEnabled);
    }
}

/// <summary>
/// The validator holds one real rule and a set of ranges. The rule is the point:
/// the admin page disables the last remaining checkbox, but a UI guard is a
/// convenience, not a constraint.
/// </summary>
public class UpdatePaymentSettingsRequestValidatorTests
{
    private readonly UpdatePaymentSettingsRequestValidator _validator = new();

    private static UpdatePaymentSettingsRequest Valid() => new()
    {
        CashEnabled = true,
        CardEnabled = false,
        DefaultMethod = PaymentMethodNames.Cash,
        ChallengeAboveFarePence = 2500,
        ReverifyAfterDormantDays = 0,
        MaxSavedCardsPerCustomer = 5,
        MaxCardAddAttemptsPerDay = 5,
        BlockBookingWhenDebtExceedsPence = 0,
    };

    [Fact]
    public void ADefaultRequest_Passes()
        => _validator.TestValidate(Valid()).ShouldNotHaveAnyValidationErrors();

    [Fact]
    public void BothMethodsOff_IsRejected()
    {
        var req = Valid();
        req.CashEnabled = false;
        req.CardEnabled = false;

        // Asserted through Validate, not TestValidate: this is a model-level
        // rule, so the error carries an empty property name.
        var result = _validator.Validate(req);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage.Contains("at least one", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("Cash")]
    [InlineData("Card")]
    public void KnownMethodNames_Pass(string method)
    {
        var req = Valid();
        req.CardEnabled = true;
        req.DefaultMethod = method;

        _validator.TestValidate(req).ShouldNotHaveValidationErrorFor(x => x.DefaultMethod);
    }

    [Theory]
    [InlineData("cash")]   // the wire spelling is capitalised; it must match Trip.PaymentMethod
    [InlineData("Bitcoin")]
    [InlineData("")]
    public void UnknownMethodNames_AreRejected(string method)
    {
        var req = Valid();
        req.DefaultMethod = method;

        _validator.TestValidate(req).ShouldHaveValidationErrorFor(x => x.DefaultMethod);
    }

    [Fact]
    public void DefaultNamingADisabledMethod_IsAllowedThrough()
    {
        // Deliberate. Turning cash off while cash is the default is an ordinary
        // thing to do, and a 400 would only make the admin do it in two steps.
        // The service corrects the default instead - see PaymentSettingsService.
        var req = Valid();
        req.CashEnabled = false;
        req.CardEnabled = true;
        req.DefaultMethod = PaymentMethodNames.Cash;

        _validator.TestValidate(req).ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(21)]
    [InlineData(-1)]
    public void ASavedCardLimitOutsideTheRange_IsRejected(int limit)
    {
        // Zero is the one that matters: it would lock every customer out of
        // paying by card, and reads as "unlimited" to anyone typing it.
        var req = Valid();
        req.MaxSavedCardsPerCustomer = limit;

        _validator.TestValidate(req).ShouldHaveValidationErrorFor(x => x.MaxSavedCardsPerCustomer);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(51)]
    public void ACardAddAttemptLimitOutsideTheRange_IsRejected(int limit)
    {
        var req = Valid();
        req.MaxCardAddAttemptsPerDay = limit;

        _validator.TestValidate(req).ShouldHaveValidationErrorFor(x => x.MaxCardAddAttemptsPerDay);
    }

    [Fact]
    public void ZeroIsValidForTheThresholds_BecauseZeroMeansSomething()
    {
        // 0 fare = always challenge; 0 dormant days = off; 0 debt = block on any
        // debt at all. None of these are "unset".
        var req = Valid();
        req.ChallengeAboveFarePence = 0;
        req.ReverifyAfterDormantDays = 0;
        req.BlockBookingWhenDebtExceedsPence = 0;

        _validator.TestValidate(req).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void NegativeThresholds_AreRejected()
    {
        var req = Valid();
        req.ChallengeAboveFarePence = -1;
        req.ReverifyAfterDormantDays = -1;
        req.BlockBookingWhenDebtExceedsPence = -1;

        var result = _validator.TestValidate(req);
        result.ShouldHaveValidationErrorFor(x => x.ChallengeAboveFarePence);
        result.ShouldHaveValidationErrorFor(x => x.ReverifyAfterDormantDays);
        result.ShouldHaveValidationErrorFor(x => x.BlockBookingWhenDebtExceedsPence);
    }
}

/// <summary>
/// The public endpoint is anonymous. What it does NOT carry is a security
/// decision, so it is worth a test rather than a comment: publishing the limits
/// hands an attacker the shape of everything they need to stay under.
/// </summary>
public class PaymentSettingsResponseShapeTests
{
    [Fact]
    public void ThePublicResponse_CarriesOnlyTheThreeMethodFields()
    {
        var properties = typeof(PaymentSettingsResponse)
            .GetProperties()
            .Select(p => p.Name)
            .Where(n => n != "EqualityContract")
            .OrderBy(n => n)
            .ToArray();

        Assert.Equal(
            new[] { "CardEnabled", "CashEnabled", "DefaultMethod" },
            properties);
    }

    [Fact]
    public void TheAdminResponse_CarriesEverySettingOnTheModel()
    {
        var model = typeof(PaymentSettings).GetProperties().Select(p => p.Name).ToHashSet();
        var response = typeof(AdminPaymentSettingsResponse).GetProperties().Select(p => p.Name).ToHashSet();

        // Anything added to the model and not surfaced to the admin portal is a
        // setting nobody can see or change - worse than not having it.
        Assert.Empty(model.Except(response));
    }
}
