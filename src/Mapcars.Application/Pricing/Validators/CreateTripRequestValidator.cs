using FluentValidation;
using Mapcars.Application.Pricing.Dtos;

namespace Mapcars.Application.Pricing.Validators;

public class CreateTripRequestValidator : AbstractValidator<CreateTripRequest>
{
    public CreateTripRequestValidator()
    {
        RuleFor(x => x.PickupAddress).NotEmpty().MaximumLength(500);
        RuleFor(x => x.DropoffAddress).NotEmpty().MaximumLength(500);
        RuleFor(x => x.PickupLat).InclusiveBetween(-90, 90);
        RuleFor(x => x.PickupLng).InclusiveBetween(-180, 180);
        RuleFor(x => x.DropoffLat).InclusiveBetween(-90, 90);
        RuleFor(x => x.DropoffLng).InclusiveBetween(-180, 180);
        RuleFor(x => x.RideOptionId).NotEmpty().MaximumLength(30);
        RuleFor(x => x.DistanceMiles).GreaterThanOrEqualTo(0);
        RuleFor(x => x.DurationMinutes).GreaterThanOrEqualTo(0);
        RuleFor(x => x.PromoCode).MaximumLength(40);
        RuleFor(x => x.PaymentMethodId).MaximumLength(100);
    }
}
