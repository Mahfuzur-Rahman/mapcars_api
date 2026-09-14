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

        // Ranges, not opinions. The right values are a business call an admin can
        // tune; these only stop a typo becoming an outage - a zero card limit
        // would lock every customer out of paying.
        RuleFor(x => x.MaxSavedCardsPerCustomer).InclusiveBetween(1, 20);
        RuleFor(x => x.MaxCardAddAttemptsPerDay).InclusiveBetween(1, 50);
        RuleFor(x => x.ChallengeAboveFarePence).InclusiveBetween(0, 100_000);
        RuleFor(x => x.ReverifyAfterDormantDays).InclusiveBetween(0, 3650);
        RuleFor(x => x.BlockBookingWhenDebtExceedsPence).InclusiveBetween(0, 100_000);

        // Deliberately NOT validated: that DefaultMethod names an enabled method.
        // Turning cash off while cash is the default is an ordinary thing for an
        // admin to do, and rejecting it with a 400 would only make them do it in
        // two steps. The service corrects the default instead, so no client is
        // ever handed a default it cannot offer.
    }
}
