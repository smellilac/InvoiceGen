namespace InvoiceGen.Api.Common;

public static class CorsExtensions
{
    public const string PolicyName = "frontend";

    // Allowed origins come from config ("Cors:AllowedOrigins": ["https://app.example.com"]).
    // Falls back to common local dev origins (Vite 5173 / Next-CRA 3000) so a local frontend
    // works with no config. In production you MUST set the real origin(s).
    //
    // No AllowCredentials: auth uses Bearer tokens in the Authorization header, not cookies —
    // so any header/method from the allowed origins is enough. Add AllowCredentials only if you
    // switch to cookie auth (and then you can't use AllowAnyOrigin).
    public static IServiceCollection AddFrontendCors(this IServiceCollection services, IConfiguration configuration)
    {
        var origins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>();
        if (origins is null || origins.Length == 0)
            origins = ["http://localhost:5173", "http://localhost:3000"];

        services.AddCors(options =>
        {
            options.AddPolicy(PolicyName, policy =>
                policy.WithOrigins(origins)
                      .AllowAnyHeader()
                      .AllowAnyMethod());
        });

        return services;
    }
}
