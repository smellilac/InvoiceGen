using InvoiceGen.Application.Features.Auth;
using InvoiceGen.Domain.Entities;
using InvoiceGen.Infrastructure.Auth;
using InvoiceGen.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace InvoiceGen.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInvoiceGenInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("Default")));

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

        return services;
    }
}
