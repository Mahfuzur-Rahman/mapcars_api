using FluentValidation;
using Mapcars.Application.Settings.Dtos;

namespace Mapcars.Application.Settings.Validators;

public class AppVersionsDocumentValidator : AbstractValidator<AppVersionsDocument>
{
    public AppVersionsDocumentValidator()
    {
        RuleFor(x => x.CustomerAndroid).NotNull().SetValidator(new AppVersionEntryValidator());
        RuleFor(x => x.CustomerIos).NotNull().SetValidator(new AppVersionEntryValidator());
        RuleFor(x => x.DriverAndroid).NotNull().SetValidator(new AppVersionEntryValidator());
        RuleFor(x => x.DriverIos).NotNull().SetValidator(new AppVersionEntryValidator());
    }
}

public class AppVersionEntryValidator : AbstractValidator<AppVersionEntryDto>
{
    public AppVersionEntryValidator()
    {
        // The apps parse this as dotted numbers, so a typo like "1.2.x" would be
        // read as "no update" on every phone. Reject it here instead.
        RuleFor(x => x.Latest)
            .NotNull()
            .Matches(@"^(\d{1,4}(\.\d{1,4}){0,3})?$")
            .WithMessage("Version must be numbers separated by dots, like 1.2.0 (or empty for none).");

        // https only: this is opened in the user's browser / store from inside
        // the app, so it must never be a javascript: or file: link.
        RuleFor(x => x.StoreUrl)
            .NotNull()
            .MaximumLength(500)
            .Must(u => string.IsNullOrEmpty(u)
                       || (Uri.TryCreate(u, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps))
            .WithMessage("Store link must be an https:// link (or empty).");

        RuleFor(x => x.Message).NotNull().MaximumLength(200);

        // An announced version with nowhere to send the user is a dead button.
        RuleFor(x => x.StoreUrl)
            .NotEmpty()
            .When(x => !string.IsNullOrWhiteSpace(x.Latest))
            .WithMessage("Add the store link for this version, or clear the version.");
    }
}
