using FieldOps.Domain.Common;
using FluentValidation;

namespace FieldOps.Api.Common;

/// <summary>Validates the first <typeparamref name="T"/> argument with its FluentValidation validator.</summary>
public sealed class ValidationFilter<T>(IValidator<T> validator) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var argument = context.Arguments.OfType<T>().FirstOrDefault();
        if (argument is null) return await next(context);

        var result = await validator.ValidateAsync(argument, context.HttpContext.RequestAborted);
        if (result.IsValid) return await next(context);

        return result.ToProblem();
    }
}

public static class ValidationFilterExtensions
{
    public static IResult ToProblem(this FluentValidation.Results.ValidationResult result) =>
        Error.Validation("Validation", "One or more validation errors occurred.", result.ToDictionary().AsReadOnly()).ToProblem();

    public static RouteHandlerBuilder Validate<T>(this RouteHandlerBuilder builder) =>
        builder.AddEndpointFilter<ValidationFilter<T>>();
}
