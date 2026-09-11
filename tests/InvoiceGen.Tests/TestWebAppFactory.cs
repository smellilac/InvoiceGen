using InvoiceGen.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using Xunit;

namespace InvoiceGen.Tests;

// Spins up a throwaway PostgreSQL container (same engine as production) for the
// test run, points the app's AppDbContext at it, and applies the real EF
// migrations — so tests exercise the actual database and migrations, not a
// SQLite stand-in. Requires a running Docker daemon.
public class TestWebAppFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine")
        .Build();

    public TestWebAppFactory()
    {
        // Env vars reach the startup-time configuration (unlike ConfigureAppConfiguration
        // in minimal hosting). "__" is the nesting separator, so Jwt__Key => Jwt:Key.
        Environment.SetEnvironmentVariable("Jwt__Key", "test-signing-key-that-is-definitely-long-enough-123456");
        Environment.SetEnvironmentVariable("Jwt__Issuer", "invoicegen");
        Environment.SetEnvironmentVariable("Jwt__Audience", "invoicegen");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing"); // disables the per-IP auth rate limiter (see Program.cs)

        builder.ConfigureTestServices(services =>
        {
            var toRemove = services.Where(d =>
                d.ServiceType == typeof(DbContextOptions<AppDbContext>) ||
                d.ServiceType == typeof(AppDbContext) ||
                d.ServiceType.Name.Contains("IDbContextOptionsConfiguration")).ToList();
            foreach (var d in toRemove) services.Remove(d);

            services.AddDbContext<AppDbContext>(o => o.UseNpgsql(_postgres.GetConnectionString()));
        });
    }

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        // Accessing Services builds the host (with the container connection string above),
        // then apply migrations to the fresh database.
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();
    }

    // Explicit: xUnit's IAsyncLifetime.DisposeAsync returns Task, while the base
    // WebApplicationFactory.DisposeAsync returns ValueTask — the explicit impl
    // disambiguates and disposes both.
    async Task IAsyncLifetime.DisposeAsync()
    {
        await _postgres.DisposeAsync();
        await base.DisposeAsync();
    }
}
