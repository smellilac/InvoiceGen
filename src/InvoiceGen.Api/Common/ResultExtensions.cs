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
