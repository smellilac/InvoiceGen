using InvoiceGen.Application.Features.DocumentTypes;
using Microsoft.AspNetCore.Http.HttpResults;

namespace InvoiceGen.Api.Features.DocumentTypes;

public static class DocumentTypeEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/document-types")
            .WithTags("document-types");
        
        MapList(group);
    }
    
    private static void MapList(RouteGroupBuilder group)
    {
        group.MapGet("/",async (
            ListDocumentTypesHandler handler,
            CancellationToken cancellationToken) =>
            {
                var result = await handler.HandleAsync(cancellationToken);
                return TypedResults.Ok(result.Value);
            })
            .WithName("ListDocumentTypes")
            .WithSummary("List available document types for the main page picker")
            .Produces<DocumentTypeDto[]>(200)
            .AllowAnonymous();
    }
}