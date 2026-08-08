using FluentValidation;
using Mapcars.Application.ErrorLogs.Dtos;

namespace Mapcars.Application.ErrorLogs.Validators;

/// <summary>
/// Deliberately permissive: this endpoint is how a broken client tells us it's
/// broken, so rejecting a report for being untidy would lose the very thing we
/// want. Only the two fields the row can't exist without are enforced —
/// everything longer than its column is truncated in the service, not refused.
/// </summary>
public class ReportErrorRequestValidator : AbstractValidator<ReportErrorRequest>
{
    public ReportErrorRequestValidator()
    {
        RuleFor(x => x.Source)
            .NotEmpty().WithMessage("Source is required.")
            .MaximumLength(20);

        RuleFor(x => x.Message)
            .NotEmpty().WithMessage("Message is required.");
    }
}
