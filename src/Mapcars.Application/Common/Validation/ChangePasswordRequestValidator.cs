using FluentValidation;
using Mapcars.Application.Common.Dtos;

namespace Mapcars.Application.Common.Validation;

/// <summary>Shared across admin, rider, and driver — see <see cref="ChangePasswordRequest"/>.</summary>
public class ChangePasswordRequestValidator : AbstractValidator<ChangePasswordRequest>
{
    public ChangePasswordRequestValidator()
    {
        RuleFor(x => x.CurrentPassword).NotEmpty().WithMessage("Current password is required.");
        RuleFor(x => x.NewPassword).StrongPassword();
    }
}
