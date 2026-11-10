using FluentValidation;

namespace MouseDent.Api.Common;

/// <summary>
/// Valida el argumento <typeparamref name="T"/> del endpoint con FluentValidation
/// y devuelve un ProblemDetails de validación si hay errores.
/// </summary>
public sealed class ValidationFilter<T>(IValidator<T> validator) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var model = context.Arguments.OfType<T>().FirstOrDefault();
        if (model is null)
        {
            return TypedResults.BadRequest();
        }

        var result = await validator.ValidateAsync(model, context.HttpContext.RequestAborted);
        if (result.IsValid)
        {
            return await next(context);
        }

        var errors = result.Errors
            .GroupBy(error => error.PropertyName)
            .ToDictionary(
                group => group.Key,
                group => group.Select(error => error.ErrorMessage).ToArray());

        return TypedResults.ValidationProblem(errors);
    }
}
