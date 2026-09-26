using FluentValidation;
using Mapcars.Application.Common.Validation;
using Mapcars.Application.Contact.Dtos;
using Mapcars.Application.Contact.Services;

namespace Mapcars.Application.Contact.Validators;

public class ContactRequestValidator : AbstractValidator<ContactRequest>
{
    public ContactRequestValidator()
    {
        RuleFor(x => x.FirstName).NotEmpty().WithMessage("First name is required.").MaximumLength(100);
        RuleFor(x => x.LastName).NotEmpty().WithMessage("Last name is required.").MaximumLength(100);
        RuleFor(x => x.Email).Email();
        RuleFor(x => x.Subject)
            .Must(s => ContactService.Subjects.ContainsKey(s ?? ""))
            .WithMessage("Please choose a subject.");
        RuleFor(x => x.Message)
            .NotEmpty().WithMessage("Message is required.")
            .MaximumLength(5000).WithMessage("Message must be 5000 characters or fewer.");
        RuleFor(x => x.Website).MaximumLength(500);
    }
}
