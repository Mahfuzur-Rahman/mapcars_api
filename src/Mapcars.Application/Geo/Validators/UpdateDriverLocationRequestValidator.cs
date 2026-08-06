using FluentValidation;
using Mapcars.Application.Geo.Dtos;

namespace Mapcars.Application.Geo.Validators;

/// <summary>Runs at the API boundary via the global ValidationActionFilter.</summary>
public class UpdateDriverLocationRequestValidator : AbstractValidator<UpdateDriverLocationRequest>
{
    public UpdateDriverLocationRequestValidator()
    {
        RuleFor(x => x.Lat).InclusiveBetween(-90, 90);
        RuleFor(x => x.Lng).InclusiveBetween(-180, 180);
        RuleFor(x => x.Heading).InclusiveBetween(0, 360).When(x => x.Heading.HasValue);
    }
}
