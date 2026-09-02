using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using InvoiceGen.Api.Common;
using InvoiceGen.Application.Common;
using InvoiceGen.Infrastructure;
using Microsoft.AspNetCore.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();

// Match the OpenAPI contract's snake_case field names (access_token, business_name, ...)
// and snake_case string enums (type: "credit_note", status: "generated").
builder.Services.ConfigureHttpJsonOptions(o =>
{
    o.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
    o.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower));
});

// Per-IP throttle for the auth endpoints (login/register/refresh brute-force protection).
// Complements per-account lockout, which doesn't stop spraying one password across many accounts.
// Disabled under "Testing" so the suite's many same-IP auth calls aren't throttled.
var rateLimitingEnabled = !builder.Environment.IsEnvironment("Testing");
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
    });
}

builder.Services.AddInvoiceGenApplicationLayer();
builder.Services.AddInvoiceGenInfrastructureLayer(builder.Configuration);
builder.Services.AddJwtBearerAuthentication(builder.Configuration);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseExceptionHandler();
app.UseHttpsRedirection();
if (rateLimitingEnabled)
    app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapEndpoints();

app.Run();

public partial class Program { }
