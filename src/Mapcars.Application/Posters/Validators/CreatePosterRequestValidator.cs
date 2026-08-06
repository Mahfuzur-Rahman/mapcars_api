using FluentValidation;
using Mapcars.Application.Posters.Dtos;

namespace Mapcars.Application.Posters.Validators;

public class CreatePosterRequestValidator : AbstractValidator<CreatePosterRequest>
{
    public CreatePosterRequestValidator()
    {
        RuleFor(x => x.Title).MaximumLength(200);
        RuleFor(x => x.Subtitle).MaximumLength(300);
        RuleFor(x => x.LinkUrl).MaximumLength(2048)
            .Must(url => Uri.TryCreate(url, UriKind.Absolute, out _))
            .WithMessage("Link URL must be a valid absolute URL.")
            .When(x => !string.IsNullOrWhiteSpace(x.LinkUrl));
        RuleFor(x => x.SortOrder).GreaterThanOrEqualTo(0);
    }
}
