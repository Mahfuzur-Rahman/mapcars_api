using FluentValidation;
using Mapcars.Application.Admins.Dtos;

namespace Mapcars.Application.Admins.Validators;

public class CreateAdminRequestValidator : AbstractValidator<CreateAdminRequest>
{
    public CreateAdminRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(255);
        RuleFor(x => x.Password)
            .NotEmpty()
            .MinimumLength(8)
            .Matches(@"[A-Z]").WithMessage("Password must contain at least one uppercase letter.")
            .Matches(@"[0-9]").WithMessage("Password must contain at least one digit.");
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.RoleId).GreaterThan(0);
    }
}
