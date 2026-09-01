using InvoiceGen.Application.Features.Auth;
using InvoiceGen.Application.Features.DocumentTypes;
using Microsoft.Extensions.DependencyInjection;

namespace InvoiceGen.Application.Common;

public static class DependencyInjection
{
    public static IServiceCollection AddInvoiceGenApplicationLayer(this IServiceCollection services)
    {
        services.AddScoped<ListDocumentTypesHandler>();

        services.AddScoped<RegisterHandler>();
        services.AddScoped<LoginHandler>();
        services.AddScoped<RefreshHandler>();
        services.AddScoped<LogoutHandler>();
        services.AddScoped<GetMeHandler>();
        services.AddScoped<UpdateMeHandler>();

        return services;
    }
}
