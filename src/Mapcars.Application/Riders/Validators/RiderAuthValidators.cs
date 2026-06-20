using FluentValidation;
using Mapcars.Application.Common.Validation;
using Mapcars.Application.Riders.Dtos;

namespace Mapcars.Application.Riders.Validators;

public class PhoneRequestValidator : AbstractValidator<PhoneRequest>
{
    public PhoneRequestValidator() => RuleFor(x => x.Phone).Phone();
}

public class VerifyOtpRequestValidator : AbstractValidator<VerifyOtpRequest>
{
    public VerifyOtpRequestValidator()
    {
        RuleFor(x => x.Phone).Phone();
        RuleFor(x => x.Code).OtpCode();
    }
}

public class EmailSignUpRequestValidator : AbstractValidator<EmailSignUpRequest>
{
    public EmailSignUpRequestValidator()
    {
        RuleFor(x => x.Email).Email();
        RuleFor(x => x.Password).StrongPassword();
        RuleFor(x => x.FullName).FullName();
    }
}

public class VerifyEmailRequestValidator : AbstractValidator<VerifyEmailRequest>
{
    public VerifyEmailRequestValidator()
    {
        RuleFor(x => x.Email).Email();
        RuleFor(x => x.Code).OtpCode();
    }
}

public class EmailLoginRequestValidator : AbstractValidator<EmailLoginRequest>
{
    public EmailLoginRequestValidator()
    {
        RuleFor(x => x.Email).Email();
        RuleFor(x => x.Password).NotEmpty().WithMessage("Password is required.");
    }
}

public class GoogleAuthRequestValidator : AbstractValidator<GoogleAuthRequest>
{
    public GoogleAuthRequestValidator() =>
        RuleFor(x => x.IdToken).NotEmpty().WithMessage("Google ID token is required.");
}

public class UpdateProfileRequestValidator : AbstractValidator<UpdateProfileRequest>
{
    public UpdateProfileRequestValidator()
    {
        RuleFor(x => x.FullName).FullName();
        When(x => !string.IsNullOrWhiteSpace(x.Email), () =>
            RuleFor(x => x.Email!).Email());
    }
}
