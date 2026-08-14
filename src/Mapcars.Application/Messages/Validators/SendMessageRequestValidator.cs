using FluentValidation;
using Mapcars.Application.Messages.Dtos;

namespace Mapcars.Application.Messages.Validators;

/// <summary>
/// Runs automatically at the API boundary via the global ValidationActionFilter.
/// </summary>
public class SendMessageRequestValidator : AbstractValidator<SendMessageRequest>
{
    public SendMessageRequestValidator()
    {
        RuleFor(x => x.Content).NotEmpty().WithMessage("Message content is required.")
            .MaximumLength(2000).WithMessage("Message must not exceed 2000 characters.");
    }
}
