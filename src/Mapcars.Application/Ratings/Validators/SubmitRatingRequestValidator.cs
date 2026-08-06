using FluentValidation;
using Mapcars.Application.Ratings.Dtos;

namespace Mapcars.Application.Ratings.Validators;

/// <summary>
/// Runs automatically at the API boundary via the global ValidationActionFilter.
/// </summary>
public class SubmitRatingRequestValidator : AbstractValidator<SubmitRatingRequest>
{
    public SubmitRatingRequestValidator()
    {
        RuleFor(x => x.Score).InclusiveBetween(1, 5).WithMessage("Score must be between 1 and 5.");
        RuleFor(x => x.Comment).MaximumLength(1000);
    }
}
