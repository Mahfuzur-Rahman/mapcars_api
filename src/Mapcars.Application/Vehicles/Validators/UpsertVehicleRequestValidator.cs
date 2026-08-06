using FluentValidation;
using Mapcars.Application.Vehicles.Dtos;

namespace Mapcars.Application.Vehicles.Validators;

/// <summary>
/// Runs automatically at the API boundary via the global ValidationActionFilter.
/// The service only performs business-rule checks (e.g. plate already taken).
/// </summary>
public class UpsertVehicleRequestValidator : AbstractValidator<UpsertVehicleRequest>
{
    // Reasonable bounds for a car registered/operating today.
    private const int EarliestYear = 1990;

    public UpsertVehicleRequestValidator()
    {
        RuleFor(x => x.Make).NotEmpty().MaximumLength(60);
        RuleFor(x => x.Model).NotEmpty().MaximumLength(60);
        RuleFor(x => x.Colour).NotEmpty().MaximumLength(40);
        RuleFor(x => x.Year)
            .InclusiveBetween(EarliestYear, 2100)
            .WithMessage($"Year must be between {EarliestYear} and 2100.");
        RuleFor(x => x.RegistrationNumber)
            .NotEmpty().WithMessage("Registration number is required.")
            .MaximumLength(15)
            .Matches("^[A-Za-z0-9 ]+$").WithMessage("Registration number may contain only letters, numbers and spaces.");
        RuleFor(x => x.PhvLicencePlateNumber).MaximumLength(30);
        RuleFor(x => x.PhvLicensingAuthority).MaximumLength(120);
    }
}
