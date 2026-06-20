using FluentValidation;
using Mapcars.Application.Common.Validation;
using Mapcars.Application.Drivers.Dtos;

namespace Mapcars.Application.Drivers.Validators;

public class DriverPhoneRequestValidator : AbstractValidator<DriverPhoneRequest>
{
    public DriverPhoneRequestValidator() => RuleFor(x => x.Phone).Phone();
}

public class DriverVerifyOtpRequestValidator : AbstractValidator<DriverVerifyOtpRequest>
{
    public DriverVerifyOtpRequestValidator()
    {
        RuleFor(x => x.Phone).Phone();
        RuleFor(x => x.Code).OtpCode();
    }
}

public class DriverEmailSignUpRequestValidator : AbstractValidator<DriverEmailSignUpRequest>
{
    public DriverEmailSignUpRequestValidator()
    {
        RuleFor(x => x.Email).Email();
        RuleFor(x => x.Password).StrongPassword();
        RuleFor(x => x.FullName).FullName();
    }
}

public class DriverVerifyEmailRequestValidator : AbstractValidator<DriverVerifyEmailRequest>
{
    public DriverVerifyEmailRequestValidator()
    {
        RuleFor(x => x.Email).Email();
        RuleFor(x => x.Code).OtpCode();
    }
}

public class DriverEmailLoginRequestValidator : AbstractValidator<DriverEmailLoginRequest>
{
    public DriverEmailLoginRequestValidator()
    {
        RuleFor(x => x.Email).Email();
        RuleFor(x => x.Password).NotEmpty().WithMessage("Password is required.");
    }
}

public class DriverGoogleAuthRequestValidator : AbstractValidator<DriverGoogleAuthRequest>
{
    public DriverGoogleAuthRequestValidator() =>
        RuleFor(x => x.IdToken).NotEmpty().WithMessage("Google ID token is required.");
}
