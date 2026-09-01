using System.Text;
using System.Text.Json;
using InvoiceGen.Api.Common;
using InvoiceGen.Application.Common;
using InvoiceGen.Infrastructure;
using InvoiceGen.Infrastructure.Auth;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();

// Match the OpenAPI contract's snake_case field names (access_token, business_name, ...).
builder.Services.ConfigureHttpJsonOptions(o =>
    o.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower);

builder.Services.AddInvoiceGenApplicationLayer();
builder.Services.AddInvoiceGenInfrastructure(builder.Configuration);

var jwt = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
    ?? throw new InvalidOperationException("Missing 'Jwt' configuration section.");

// Fail fast with a clear message instead of a cryptic error on the first auth request.
if (Encoding.UTF8.GetByteCount(jwt.Key) < 32)
    throw new InvalidOperationException(
        "Jwt:Key must be at least 32 bytes (256 bits) for HMAC-SHA256. " +
        "Set it via user-secrets in development or environment variables in production.");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
            ClockSkew = TimeSpan.Zero
        };
    });
builder.Services.AddAuthorization();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseExceptionHandler();
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapEndpoints();

app.Run();

public partial class Program { }
