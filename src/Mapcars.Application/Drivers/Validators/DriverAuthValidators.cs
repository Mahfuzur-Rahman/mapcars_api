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

public class UpdateDriverProfileRequestValidator : AbstractValidator<UpdateDriverProfileRequest>
{
    public UpdateDriverProfileRequestValidator()
    {
        RuleFor(x => x.FirstName).NotEmpty().WithMessage("First name is required.").MaximumLength(100);
        RuleFor(x => x.LastName).MaximumLength(100);
        When(x => !string.IsNullOrWhiteSpace(x.Email), () =>
            RuleFor(x => x.Email!).Email());
        RuleFor(x => x.Address).MaximumLength(500);
        RuleFor(x => x.NationalIdNumber)
            .NotEmpty().WithMessage("National ID number is required.")
            .MaximumLength(50);
        RuleFor(x => x.DateOfBirth)
            .Must(dob => dob is null || dob.Value <= DateOnly.FromDateTime(DateTime.UtcNow))
            .WithMessage("Date of birth cannot be in the future.");
        RuleFor(x => x.DrivingLicenceNumber).MaximumLength(20);
        RuleFor(x => x.PassportNumber).MaximumLength(50);
        RuleFor(x => x.EmergencyContactName).MaximumLength(200);
        When(x => !string.IsNullOrWhiteSpace(x.EmergencyContactPhone), () =>
            RuleFor(x => x.EmergencyContactPhone!).Phone());
    }
}
