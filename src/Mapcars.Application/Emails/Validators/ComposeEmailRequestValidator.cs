using FluentValidation;
using Mapcars.Application.Common.Validation;
using Mapcars.Application.Emails.Dtos;

namespace Mapcars.Application.Emails.Validators;

/// <summary>
/// The Compose form only offers 5 @mapcars.uk addresses, but the API can't
/// trust that — <see cref="ComposeEmailRequest.FromAddress"/> is checked
/// server-side so a tampered request can't send mail as an arbitrary sender.
/// </summary>
public class ComposeEmailRequestValidator : AbstractValidator<ComposeEmailRequest>
{
    public ComposeEmailRequestValidator()
    {
        RuleFor(x => x.To).Email();

        RuleFor(x => x.Subject)
            .NotEmpty().WithMessage("Subject is required.")
            .MaximumLength(500);

        RuleFor(x => x.BodyHtml)
            .NotEmpty().WithMessage("Body is required.");

        RuleFor(x => x.FromAddress)
            .NotEmpty().WithMessage("From address is required.")
            .Matches(@"^[\w.+-]+@mapcars\.uk$").WithMessage("From address must be an @mapcars.uk address.");

        RuleFor(x => x.FromName)
            .MaximumLength(200);
    }
}
