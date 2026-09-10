using ErrorOr;
using Microsoft.AspNetCore.Mvc;

namespace InvoiceGen.Api.Common;

public static class ResultExtensions
{
    public static async Task<IResult> ToProblemDetails<T>(
        this ErrorOr<T> result,
        IProblemDetailsService problemDetailsService,
        HttpContext httpContext)
    {
        var error = result.FirstError;

        // A field-level validation error (handler-side, where the rule needs data the
        // ValidationFilter can't see) carries the offending field in its metadata. Surface it
        // under the `errors` map so the response matches the ValidationFilter / OpenAPI
        // ValidationErrorResponse.fields shape rather than a bare title/detail.
        if (error.Type == ErrorType.Validation &&
            error.Metadata is { } metadata &&
            metadata.TryGetValue("field", out var fieldValue) &&
            fieldValue is string field)
        {
            return Results.ValidationProblem(
                new Dictionary<string, string[]> { [field] = [error.Description] },
                statusCode: StatusCodes.Status422UnprocessableEntity);
        }

        var statusCode = error.Type switch
        {
            ErrorType.NotFound     => StatusCodes.Status404NotFound,
            ErrorType.Conflict     => StatusCodes.Status409Conflict,
            ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
            ErrorType.Validation   => StatusCodes.Status422UnprocessableEntity,
            _                      => StatusCodes.Status500InternalServerError
        };

        httpContext.Response.StatusCode = statusCode;

        await problemDetailsService.WriteAsync(
            new ProblemDetailsContext
            {
                HttpContext = httpContext,
                ProblemDetails = new ProblemDetails
                {
                    Status = statusCode,
                    Detail = error.Description,
                    Title = error.Code
                }
            });

        return Results.Empty;
    }
}
