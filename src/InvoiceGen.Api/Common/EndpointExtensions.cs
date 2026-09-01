using InvoiceGen.Api.Features.Auth;
using InvoiceGen.Api.Features.DocumentTypes;

namespace InvoiceGen.Api.Common;

public static class EndpointExtensions
{
    public static WebApplication MapEndpoints(this WebApplication app)
    {
        DocumentTypeEndpoints.Map(app);
        AuthEndpoints.Map(app);

        return app;
    }
}
