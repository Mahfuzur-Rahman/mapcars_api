using FluentValidation;
using Mapcars.Application.Common.Dtos;

namespace Mapcars.Application.Auth.Validators;

public class AppleAuthRequestValidator : AbstractValidator<AppleAuthRequest>
{
    public AppleAuthRequestValidator()
    {
        RuleFor(x => x.IdToken).NotEmpty().WithMessage("Apple identity token is required.");
        RuleFor(x => x.Nonce).NotEmpty().WithMessage("Nonce is required.").MaximumLength(200);
        RuleFor(x => x.FullName).MaximumLength(200);
    }
}
