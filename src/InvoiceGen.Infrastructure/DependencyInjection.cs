using InvoiceGen.Application.Common;
using InvoiceGen.Application.Features.Auth;
using InvoiceGen.Application.Features.Documents;
using InvoiceGen.Domain.Entities;
using InvoiceGen.Infrastructure.Auth;
using InvoiceGen.Infrastructure.Email;
using InvoiceGen.Infrastructure.Imaging;
using InvoiceGen.Infrastructure.Pdf;
using InvoiceGen.Infrastructure.Persistence;
using InvoiceGen.Infrastructure.Search;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Pgvector.EntityFrameworkCore;

namespace InvoiceGen.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInvoiceGenInfrastructureLayer(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(
                configuration.GetConnectionString("DefaultConnection"),
                npgsql => npgsql.UseVector()));

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
        services.Configure<GoogleAuthOptions>(configuration.GetSection(GoogleAuthOptions.SectionName));
        services.TryAddSingleton(TimeProvider.System);

        services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();
        services.AddScoped<IRefreshTokenService, RefreshTokenService>();
        services.AddScoped<IGoogleTokenVerifier, GoogleTokenVerifier>();
        services.AddSingleton<IPdfRenderer, PdfRenderer>();
        services.AddSingleton<IImageProcessor, ImageSharpImageProcessor>();

        // Typed HTTP client for the external Python InvoiceGen.Search service. BaseAddress comes
        // from Search:BaseUrl; the caller's bearer token is forwarded per-request by the client.
        services.Configure<SearchOptions>(configuration.GetSection(SearchOptions.SectionName));
        services.AddHttpClient<SearchServiceClient>((sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<SearchOptions>>().Value;
            client.BaseAddress = new Uri(options.BaseUrl);
        });

        // Email delivery: in-process queue + background worker.
        services.AddSingleton<EmailQueue>();
        services.AddSingleton<IEmailQueue>(sp => sp.GetRequiredService<EmailQueue>());
        services.AddHostedService<EmailSendingWorker>();

        // Sender is chosen by environment: Development and Testing use the logging placeholder
        // (never send a real email); everything else (Staging/Production) uses the real SMTP
        // relay (e.g. Brevo). SMTP settings must be configured in those environments.
        if (environment.IsEnvironment("Testing"))
        {
            services.AddSingleton<IEmailSender, LoggingEmailSender>();
        }
        else
        {
            services.Configure<SmtpOptions>(configuration.GetSection(SmtpOptions.SectionName));
            services.AddSingleton<IEmailSender, SmtpEmailSender>();
        }

        return services;
    }
}
