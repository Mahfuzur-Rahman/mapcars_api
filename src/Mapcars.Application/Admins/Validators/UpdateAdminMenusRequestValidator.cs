using FluentValidation;
using Mapcars.Application.Admins.Dtos;

namespace Mapcars.Application.Admins.Validators;

public class UpdateAdminMenusRequestValidator : AbstractValidator<UpdateAdminMenusRequest>
{
    public UpdateAdminMenusRequestValidator()
    {
        RuleFor(x => x.MenuIds).NotNull();
        RuleForEach(x => x.MenuIds).GreaterThan(0);
    }
}
