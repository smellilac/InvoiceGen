using FluentValidation;

namespace InvoiceGen.Api.Common;

// One generic filter, reused for every request type: close it over T on each endpoint
// (.AddEndpointFilter<ValidationFilter<CreateDocumentRequest>>()). Runs the matching
// validator before the handler and returns 422 with ALL field errors if invalid.
public sealed class ValidationFilter<T> : IEndpointFilter where T : notnull
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var request = context.Arguments.OfType<T>().FirstOrDefault();
        var validator = context.HttpContext.RequestServices.GetService<IValidator<T>>();

        if (request is not null && validator is not null)
        {
            var result = await validator.ValidateAsync(request, context.HttpContext.RequestAborted);
            if (!result.IsValid)
            {
                var errors = result.Errors
                    .GroupBy(e => e.PropertyName)
                    .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());

                return Results.ValidationProblem(errors,
                    statusCode: StatusCodes.Status422UnprocessableEntity);
            }
        }

        return await next(context);
    }
}
