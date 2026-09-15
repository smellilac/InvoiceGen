using InvoiceGen.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InvoiceGen.Api.Common;

public static class MigrationExtensions
{
    // Render's free tier has no Pre-Deploy Command, so we apply EF Core migrations on startup
    // instead. Database.Migrate() is idempotent — it consults the __EFMigrationsHistory table and
    // only applies what isn't already there — so this is safe to run on every startup, including
    // the free tier's cold-start wake-ups after spin-down, not just real deploys.
    //
    // Skipped under "Testing": the integration suite manages its own schema via Testcontainers.
    public static async Task ApplyPendingMigrationsAsync(this WebApplication app)
    {
        if (app.Environment.IsEnvironment("Testing"))
            return;

        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

        var pending = (await db.Database.GetPendingMigrationsAsync()).ToList();
        if (pending.Count == 0)
        {
            logger.LogInformation("Database schema is up to date; no migrations to apply.");
            return;
        }

        logger.LogInformation(
            "Applying {Count} pending migration(s): {Migrations}",
            pending.Count,
            string.Join(", ", pending));
        await db.Database.MigrateAsync();
        logger.LogInformation("Database migrations applied successfully.");
    }
}
