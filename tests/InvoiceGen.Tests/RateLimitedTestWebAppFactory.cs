using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;

namespace InvoiceGen.Tests;

// Same as TestWebAppFactory (throwaway Postgres, "Testing" environment, LoggingEmailSender), but
// force-enables the per-IP rate limiter — which the base factory otherwise disables under Testing —
// so the guest endpoint's 429 behaviour can be exercised. Only the guest rate-limit test uses this,
// in its own class, so the tighter limit doesn't interfere with the rest of the suite.
public sealed class RateLimitedTestWebAppFactory : TestWebAppFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        // A host setting, so it reaches Program.cs's startup-time builder.Configuration read (same
        // mechanism as UseEnvironment) — not a process-wide env var that could leak into the other
        // factories building in parallel.
        builder.UseSetting("RateLimiting:Enabled", "true");
    }
}
