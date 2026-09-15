using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using InvoiceGen.Api.Common;
using InvoiceGen.Application.Common;
using InvoiceGen.Infrastructure;
using Microsoft.AspNetCore.RateLimiting;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInvoiceGenOpenApi();
builder.Services.AddProblemDetails();

// Match the OpenAPI contract's snake_case field names (access_token, business_name, ...)
// and snake_case string enums (type: "credit_note", status: "generated").
builder.Services.ConfigureHttpJsonOptions(o =>
{
    o.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
    o.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower));
});

// Per-IP throttle for the auth endpoints (login/register/refresh brute-force protection) and the
// heavier abuse-prone endpoints (email send, guest PDF rendering).
// Complements per-account lockout, which doesn't stop spraying one password across many accounts.
// Disabled under "Testing" by default so the suite's many same-IP calls aren't throttled; a test
// that specifically exercises 429 can force it on via RateLimiting:Enabled=true.
var rateLimitingEnabled = builder.Configuration.GetValue<bool?>("RateLimiting:Enabled")
    ?? !builder.Environment.IsEnvironment("Testing");
if (rateLimitingEnabled)
{
    builder.Services.AddRateLimiter(options =>
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        options.AddPolicy("auth", httpContext =>
            RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    Window = TimeSpan.FromMinutes(1),
                    PermitLimit = 20,
                    QueueLimit = 0
                }));

        // Guest document rendering (POST /documents/guest) renders a PDF synchronously per call —
        // far heavier than an auth check — so it gets its own, tighter per-IP window (10/min).
        // Purely a cost/abuse guard; unrelated to any product "free documents" limit. See
        // x-guest-document-policy.
        options.AddPolicy("guest", httpContext =>
            RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    Window = TimeSpan.FromMinutes(1),
                    PermitLimit = 10,
                    QueueLimit = 0
                }));
    });
}

builder.Services.AddFrontendCors(builder.Configuration);
builder.Services.AddInvoiceGenApplicationLayer();
builder.Services.AddInvoiceGenInfrastructureLayer(builder.Configuration, builder.Environment);
builder.Services.AddJwtBearerAuthentication(builder.Configuration);

var app = builder.Build();

// Apply any pending EF Core migrations before serving requests (Render free tier has no
// Pre-Deploy Command). Idempotent and skipped in the Testing environment. See MigrationExtensions.
await app.ApplyPendingMigrationsAsync();

// API docs are development-only — the OpenAPI document exposes internal structure and the
// Scalar "Try It" panel would let anyone exercise the API. Gate both behind auth if ever
// exposed in another environment.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(options =>
    {
        options
            .WithTitle("InvoiceGen API")
            .AddPreferredSecuritySchemes("Bearer");
    });
}

app.UseExceptionHandler();
app.UseHttpsRedirection();
app.UseCors(CorsExtensions.PolicyName);
if (rateLimitingEnabled)
    app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapEndpoints();

app.Run();

public partial class Program { }
