using FluentValidation;
using Mapcars.Application.Common.Dtos;

namespace Mapcars.Application.Common.Validation;

public class VerifyProfileEmailRequestValidator : AbstractValidator<VerifyProfileEmailRequest>
{
    public VerifyProfileEmailRequestValidator()
    {
        RuleFor(x => x.Code).OtpCode();
    }
}
