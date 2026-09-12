using FluentValidation;
using InvoiceGen.Application.Features.Auth;
using InvoiceGen.Application.Features.Customers;
using InvoiceGen.Application.Features.Documents;
using InvoiceGen.Application.Features.DocumentTypes;
using Microsoft.Extensions.DependencyInjection;

namespace InvoiceGen.Application.Common;

public static class DependencyInjection
{
    public static IServiceCollection AddInvoiceGenApplicationLayer(this IServiceCollection services)
    {
        services.AddDocumentTypesHandlers();
        services.AddAuthHandlers();
        services.AddDocumentsHandlers();
        services.AddCustomersHandlers();

        return services;
    }

    private static void AddDocumentTypesHandlers(this IServiceCollection services)
    {
        services.AddScoped<ListDocumentTypesHandler>();
    }

    private static void AddAuthHandlers(this IServiceCollection services)
    {
        services.AddScoped<AuthTokenIssuer>();
        services.AddScoped<RegisterHandler>();
        services.AddScoped<LoginHandler>();
        services.AddScoped<GoogleSignInHandler>();
        services.AddScoped<RefreshHandler>();
        services.AddScoped<LogoutHandler>();
        services.AddScoped<GetMeHandler>();
        services.AddScoped<UpdateMeHandler>();
        services.AddScoped<DeleteAccountHandler>();
        services.AddScoped<UploadLogoHandler>();
        services.AddScoped<DeleteLogoHandler>();
        services.AddScoped<GetLogoHandler>();
    }

    private static void AddDocumentsHandlers(this IServiceCollection services)
    {
        services.AddScoped<CreateDocumentHandler>();
        services.AddScoped<CreateGuestDocumentHandler>();
        services.AddScoped<ListDocumentsHandler>();
        services.AddScoped<GetDocumentHandler>();
        services.AddScoped<DeleteDocumentHandler>();
        services.AddScoped<GetDocumentPdfHandler>();
        services.AddScoped<SendDocumentHandler>();
        services.AddScoped<RecordSettlementHandler>();

        services.AddScoped<IValidator<CreateDocumentRequest>, CreateDocumentRequestValidator>();
        services.AddScoped<IValidator<GuestCreateDocumentRequest>, GuestCreateDocumentRequestValidator>();
        services.AddScoped<IValidator<SendDocumentRequest>, SendDocumentRequestValidator>();
    }

    private static void AddCustomersHandlers(this IServiceCollection services)
    {
        services.AddScoped<CreateCustomerHandler>();
        services.AddScoped<ListCustomersHandler>();
        services.AddScoped<GetCustomerHandler>();
        services.AddScoped<UpdateCustomerHandler>();
        services.AddScoped<DeleteCustomerHandler>();

        services.AddScoped<IValidator<CreateCustomerRequest>, CreateCustomerRequestValidator>();
        services.AddScoped<IValidator<UpdateCustomerRequest>, UpdateCustomerRequestValidator>();
    }
}
