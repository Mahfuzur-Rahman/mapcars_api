using FluentValidation;
using Mapcars.Application.Auth.Dtos;
using Mapcars.Application.Common.Validation;
using Mapcars.Domain.Constants;

namespace Mapcars.Application.Auth.Validators;

public class ForgotPasswordRequestValidator : AbstractValidator<ForgotPasswordRequest>
{
    public ForgotPasswordRequestValidator()
    {
        RuleFor(x => x.Email).Email();
        RuleFor(x => x.UserType)
            .Must(t => UserTypes.IsCustomer(t) || UserTypes.IsDriver(t))
            .WithMessage("UserType must be 'customer' or 'driver'.");
    }
}

public class ResetPasswordRequestValidator : AbstractValidator<ResetPasswordRequest>
{
    public ResetPasswordRequestValidator()
    {
        RuleFor(x => x.Email).Email();
        RuleFor(x => x.UserType)
            .Must(t => UserTypes.IsCustomer(t) || UserTypes.IsDriver(t))
            .WithMessage("UserType must be 'customer' or 'driver'.");
        RuleFor(x => x.Code).OtpCode();
        RuleFor(x => x.NewPassword).StrongPassword();
    }
}
