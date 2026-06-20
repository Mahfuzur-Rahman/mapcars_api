using FluentValidation;

namespace Mapcars.Application.Common.Validation;

/// <summary>
/// Reusable validation rules so every feature validates the same field the same
/// way (phone, email, OTP code, password). Keeps rules consistent across riders,
/// drivers, and admins.
/// </summary>
public static class CommonRules
{
    public static IRuleBuilderOptions<T, string> Phone<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().WithMessage("Phone number is required.")
            .MinimumLength(7).MaximumLength(20)
            .Matches(@"^\+?[0-9\s\-]+$").WithMessage("Phone number is not valid.");

    public static IRuleBuilderOptions<T, string> Email<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("Email is not valid.")
            .MaximumLength(256);

    public static IRuleBuilderOptions<T, string> OtpCode<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().WithMessage("Code is required.")
            .Matches(@"^[0-9]{6}$").WithMessage("Code must be 6 digits.");

    public static IRuleBuilderOptions<T, string> FullName<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().WithMessage("Full name is required.").MaximumLength(200);

    /// <summary>Strong password policy for sign-up / account creation.</summary>
    public static IRuleBuilderOptions<T, string> StrongPassword<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().WithMessage("Password is required.")
            .MinimumLength(8).WithMessage("Password must be at least 8 characters.")
            .Matches(@"[A-Z]").WithMessage("Password must contain at least one uppercase letter.")
            .Matches(@"[0-9]").WithMessage("Password must contain at least one digit.");
}
