using InvoiceGen.Api.Features.DocumentTypes;

namespace InvoiceGen.Api.Common;

public static class EndpointExtensions
{
    public static WebApplication MapEndpoints(this WebApplication app)
    {
        DocumentTypeEndpoints.Map(app);

        return app;
    }
}
