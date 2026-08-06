using FluentValidation;
using Mapcars.Application.Auth.Dtos;
using Mapcars.Application.Common.Validation;

namespace Mapcars.Application.Auth.Validators;

public class UnifiedLoginRequestValidator : AbstractValidator<UnifiedLoginRequest>
{
    public UnifiedLoginRequestValidator()
    {
        RuleFor(x => x.Email).Email();
        RuleFor(x => x.Password).NotEmpty().WithMessage("Password is required.");
    }
}
