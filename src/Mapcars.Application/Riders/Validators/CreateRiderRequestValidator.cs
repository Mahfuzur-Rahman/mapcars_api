using FluentValidation;
using Mapcars.Application.Riders.Dtos;

namespace Mapcars.Application.Riders.Validators;

public class CreateRiderRequestValidator : AbstractValidator<CreateRiderRequest>
{
    public CreateRiderRequestValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.PhoneNumber).NotEmpty().MaximumLength(20);
    }
}
