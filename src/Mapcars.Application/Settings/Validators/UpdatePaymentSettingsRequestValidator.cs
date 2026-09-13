using FluentValidation;
using Mapcars.Application.Settings.Dtos;
using Mapcars.Application.Settings.Models;

namespace Mapcars.Application.Settings.Validators;

public class UpdatePaymentSettingsRequestValidator : AbstractValidator<UpdatePaymentSettingsRequest>
{
    public UpdatePaymentSettingsRequestValidator()
    {
        RuleFor(x => x.DefaultMethod)
            .Must(m => m == PaymentMethodNames.Cash || m == PaymentMethodNames.Card)
            .WithMessage("Default method must be Cash or Card.");

        // The one rule a checkbox cannot enforce. The admin page disables the last
        // remaining box, but a UI guard is a convenience, not a constraint — this
        // is what actually stops a platform that accepts no payment at all.
        RuleFor(x => x)
            .Must(x => x.CashEnabled || x.CardEnabled)
            .WithMessage("At least one payment method must stay enabled.");

        // Deliberately NOT validated: that DefaultMethod names an enabled method.
        // Turning cash off while cash is the default is an ordinary thing for an
        // admin to do, and rejecting it with a 400 would only make them do it in
        // two steps. The service corrects the default instead, so no client is
        // ever handed a default it cannot offer.
    }
}
