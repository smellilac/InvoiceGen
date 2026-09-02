using InvoiceGen.Api.Features.Auth;
using InvoiceGen.Api.Features.Customers;
using InvoiceGen.Api.Features.Documents;
using InvoiceGen.Api.Features.DocumentTypes;

namespace InvoiceGen.Api.Common;

public static class EndpointExtensions
{
    public static WebApplication MapEndpoints(this WebApplication app)
    {
        DocumentTypeEndpoints.Map(app);
        AuthEndpoints.Map(app);
        DocumentEndpoints.Map(app);
        CustomerEndpoints.Map(app);

        return app;
    }
}
