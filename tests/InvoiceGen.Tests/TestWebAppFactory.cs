using InvoiceGen.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace InvoiceGen.Tests;

// Docker isn't available in this environment, so instead of Testcontainers +
// Postgres we back the app with an in-memory SQLite database (schema created
// via EnsureCreated, independent of the Npgsql migration). Also supplies the
// Jwt config the app needs to start, which normally comes from user-secrets.
public sealed class TestWebAppFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    public TestWebAppFactory()
    {
        _connection.Open();

        // Env vars are read by WebApplication.CreateBuilder into the startup-time
        // configuration (unlike ConfigureAppConfiguration in minimal hosting).
        // "__" is the nesting separator, so Jwt__Key => Jwt:Key.
        Environment.SetEnvironmentVariable("Jwt__Key", "test-signing-key-that-is-definitely-long-enough-123456");
        Environment.SetEnvironmentVariable("Jwt__Issuer", "invoicegen");
        Environment.SetEnvironmentVariable("Jwt__Audience", "invoicegen");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureTestServices(services =>
        {
            var toRemove = services.Where(d =>
                d.ServiceType == typeof(DbContextOptions<AppDbContext>) ||
                d.ServiceType == typeof(AppDbContext) ||
                d.ServiceType.Name.Contains("IDbContextOptionsConfiguration")).ToList();
            foreach (var d in toRemove) services.Remove(d);

            services.AddDbContext<AppDbContext>(o => o.UseSqlite(_connection));
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);
        using var scope = host.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreated();
        return host;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) _connection.Dispose();
    }
}
