using FluentValidation;
using Mapcars.Application.SavedPlaces.Dtos;

namespace Mapcars.Application.SavedPlaces.Validators;

/// <summary>
/// Runs automatically at the API boundary via the global ValidationActionFilter.
/// </summary>
public class UpsertSavedPlaceRequestValidator : AbstractValidator<UpsertSavedPlaceRequest>
{
    public UpsertSavedPlaceRequestValidator()
    {
        RuleFor(x => x.Label).NotEmpty().MaximumLength(40);
        RuleFor(x => x.Address).NotEmpty().MaximumLength(500);
        RuleFor(x => x.Lat).InclusiveBetween(-90, 90);
        RuleFor(x => x.Lng).InclusiveBetween(-180, 180);
    }
}
