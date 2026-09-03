using InvoiceGen.Application.Common;
using InvoiceGen.Application.Features.Auth;
using InvoiceGen.Application.Features.Documents;
using InvoiceGen.Domain.Entities;
using InvoiceGen.Infrastructure.Auth;
using InvoiceGen.Infrastructure.Email;
using InvoiceGen.Infrastructure.Pdf;
using InvoiceGen.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace InvoiceGen.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInvoiceGenInfrastructureLayer(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("Default")));

        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());

        services.AddIdentityCore<AppUser>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.Password.RequiredLength = 8;

                options.Lockout.AllowedForNewUsers = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
            })
            .AddEntityFrameworkStores<AppDbContext>();

        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.TryAddSingleton(TimeProvider.System);

        services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();
        services.AddScoped<IRefreshTokenService, RefreshTokenService>();
        services.AddSingleton<IPdfRenderer, PdfRenderer>();

        // Email delivery: in-process queue + placeholder sender + background worker.
        services.AddSingleton<EmailQueue>();
        services.AddSingleton<IEmailQueue>(sp => sp.GetRequiredService<EmailQueue>());
        services.AddSingleton<IEmailSender, LoggingEmailSender>();
        services.AddHostedService<EmailSendingWorker>();

        return services;
    }
}
