using FluentValidation;
using Microsoft.AspNetCore.Mvc.Filters;
using ValidationException = Mapcars.Application.Common.Exceptions.ValidationException;

namespace Mapcars.Api.Filters;

/// <summary>
/// Runs FluentValidation on every action argument that has a registered
/// <c>IValidator&lt;T&gt;</c>, before the action executes. This is the single,
/// uniform place input validation happens — no endpoint can forget it.
///
/// Failures are thrown as the application <see cref="ValidationException"/>, which
/// <c>ExceptionHandlingMiddleware</c> translates into a 400 problem+json response.
/// </summary>
public sealed class ValidationActionFilter(IServiceProvider services) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        foreach (var argument in context.ActionArguments.Values)
        {
            if (argument is null) continue;

            var validatorType = typeof(IValidator<>).MakeGenericType(argument.GetType());
            if (services.GetService(validatorType) is not IValidator validator) continue;

            var ctx = new ValidationContext<object>(argument);
            var result = await validator.ValidateAsync(ctx, context.HttpContext.RequestAborted);

            if (!result.IsValid)
                throw new ValidationException(result.Errors);
        }

        await next();
    }
}
