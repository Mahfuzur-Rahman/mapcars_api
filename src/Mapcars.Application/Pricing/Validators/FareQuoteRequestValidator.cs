using FluentValidation;
using Mapcars.Application.Pricing.Dtos;

namespace Mapcars.Application.Pricing.Validators;

public class FareQuoteRequestValidator : AbstractValidator<FareQuoteRequest>
{
    public FareQuoteRequestValidator()
    {
        RuleFor(x => x.PickupLat).InclusiveBetween(-90, 90);
        RuleFor(x => x.PickupLng).InclusiveBetween(-180, 180);
        RuleFor(x => x.DropoffLat).InclusiveBetween(-90, 90);
        RuleFor(x => x.DropoffLng).InclusiveBetween(-180, 180);
        RuleFor(x => x.DistanceMiles).GreaterThanOrEqualTo(0);
        RuleFor(x => x.DurationMinutes).GreaterThanOrEqualTo(0);
    }
}
