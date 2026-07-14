using FluentValidation;
using Mapcars.Application.Pricing.Models;

namespace Mapcars.Application.Pricing.Validators;

/// <summary>
/// Validates an admin-supplied fare chart before it's published. Runs at the API
/// boundary via the global ValidationActionFilter (the PUT body is a FareChart).
/// </summary>
public class FareChartValidator : AbstractValidator<FareChart>
{
    public FareChartValidator()
    {
        RuleFor(x => x.Currency).NotEmpty().Length(3);

        RuleFor(x => x.Base.MinimumFarePence).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Base.BookingFeePence).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Rates.PerMilePence).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Rates.PerMinutePence).GreaterThanOrEqualTo(0);

        RuleFor(x => x.Platform.DriverFeePercent).InclusiveBetween(0, 100);

        RuleFor(x => x.Tiers).NotEmpty().WithMessage("At least one ride tier is required.");
        RuleForEach(x => x.Tiers).ChildRules(t =>
        {
            t.RuleFor(v => v.Id).NotEmpty().MaximumLength(30);
            t.RuleFor(v => v.Name).NotEmpty().MaximumLength(60);
            t.RuleFor(v => v.BaseFarePence).GreaterThanOrEqualTo(0);
            t.RuleFor(v => v.Multiplier).GreaterThan(0);
            t.RuleFor(v => v.Capacity).GreaterThan(0);
            t.RuleFor(v => v.EtaMinutes).GreaterThanOrEqualTo(0);
        });
    }
}
