using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace InvoiceGen.Api.Common;

public static class OpenApiExtensions
{
    // Registers the OpenAPI document and teaches it about our JWT bearer scheme so the
    // generated doc advertises `Authorization: Bearer <token>`. Without this transformer
    // Scalar renders the endpoints but has no "Authorize" affordance for the "Try It" panel.
    public static IServiceCollection AddInvoiceGenOpenApi(this IServiceCollection services)
    {
        services.AddOpenApi(options =>
        {
            options.AddDocumentTransformer<BearerSecuritySchemeTransformer>();
        });

        return services;
    }

    // Adds the "Bearer" security scheme and applies it as a document-wide requirement.
    private sealed class BearerSecuritySchemeTransformer : IOpenApiDocumentTransformer
    {
        public Task TransformAsync(
            OpenApiDocument document,
            OpenApiDocumentTransformerContext context,
            CancellationToken cancellationToken)
        {
            var scheme = new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description = "Paste a JWT access token obtained from POST /auth/login."
            };

            document.Components ??= new OpenApiComponents();
            document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
            document.Components.SecuritySchemes["Bearer"] = scheme;

            document.Security ??= [];
            document.Security.Add(new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference("Bearer", document)] = []
            });

            return Task.CompletedTask;
        }
    }
}
