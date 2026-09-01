using InvoiceGen.Application.Features.DocumentTypes;
using Microsoft.Extensions.DependencyInjection;

namespace InvoiceGen.Application.Common;

public static class DependencyInjection
{
    public static IServiceCollection AddInvoiceGenApplicationLayer(this IServiceCollection services)
    {
        services.AddScoped<ListDocumentTypesHandler>();
        return services;
    }
}
