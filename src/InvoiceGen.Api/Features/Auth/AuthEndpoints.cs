using System.Security.Claims;
using InvoiceGen.Api.Common;
using InvoiceGen.Application.Features.Auth;

namespace InvoiceGen.Api.Features.Auth;

public static class AuthEndpoints
{
    // Max accepted upload size for a logo (raw bytes, before server-side normalization).
    private const long MaxLogoBytes = 5 * 1024 * 1024;

    public static void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/auth").WithTags("auth").RequireRateLimiting("auth");

        MapRegister(group);
        MapLogin(group);
        MapRefresh(group);
        MapLogout(group);
        MapGetMe(group);
        MapUpdateMe(group);
        MapDeleteMe(group);
        MapUploadLogo(group);
        MapDeleteLogo(group);

        // GET /auth/logo/{logoId} is mapped OUTSIDE the auth group on purpose: it must be
        // anonymous (an <img> can't send a bearer token) and must NOT share the /auth
        // per-IP rate limiter, which would throttle ordinary image loads.
        MapGetLogo(app);
    }

    private static void MapRegister(RouteGroupBuilder group)
    {
        group.MapPost("/register", async (
            RegisterRequest request,
            RegisterHandler handler,
            IProblemDetailsService pds,
            HttpContext http,
            CancellationToken ct) =>
        {
            var result = await handler.HandleAsync(request, ct);
            return result.IsError
                ? await result.ToProblemDetails(pds, http)
                : Results.Created((string?)null, result.Value);
        })
        .WithName("Register")
        .WithSummary("Create a new account")
        .AllowAnonymous();
    }

    private static void MapLogin(RouteGroupBuilder group)
    {
        group.MapPost("/login", async (
            LoginRequest request,
            LoginHandler handler,
            IProblemDetailsService pds,
            HttpContext http,
            CancellationToken ct) =>
        {
            var result = await handler.HandleAsync(request, ct);
            return result.IsError
                ? await result.ToProblemDetails(pds, http)
                : Results.Ok(result.Value);
        })
        .WithName("Login")
        .WithSummary("Log in with email and password")
        .AllowAnonymous();
    }

    private static void MapRefresh(RouteGroupBuilder group)
    {
        group.MapPost("/refresh", async (
            RefreshRequest request,
            RefreshHandler handler,
            IProblemDetailsService pds,
            HttpContext http,
            CancellationToken ct) =>
        {
            var result = await handler.HandleAsync(request, ct);
            return result.IsError
                ? await result.ToProblemDetails(pds, http)
                : Results.Ok(result.Value);
        })
        .WithName("Refresh")
        .WithSummary("Exchange a refresh token for a new access token")
        .AllowAnonymous();
    }

    private static void MapLogout(RouteGroupBuilder group)
    {
        group.MapPost("/logout", async (
            LogoutRequest request,
            LogoutHandler handler,
            IProblemDetailsService pds,
            HttpContext http,
            CancellationToken ct) =>
        {
            var result = await handler.HandleAsync(request, ct);
            return result.IsError
                ? await result.ToProblemDetails(pds, http)
                : Results.NoContent();
        })
        .WithName("Logout")
        .WithSummary("Revoke the current refresh token")
        .RequireAuthorization();
    }

    private static void MapGetMe(RouteGroupBuilder group)
    {
        group.MapGet("/me", async (
            ClaimsPrincipal principal,
            GetMeHandler handler,
            IProblemDetailsService pds,
            HttpContext http,
            CancellationToken ct) =>
        {
            var userId = principal.GetUserId();
            if (userId is null) return Results.Unauthorized();

            var result = await handler.HandleAsync(userId.Value, ct);
            return result.IsError
                ? await result.ToProblemDetails(pds, http)
                : Results.Ok(result.Value);
        })
        .WithName("GetCurrentUser")
        .WithSummary("Get the logged-in user's profile")
        .RequireAuthorization();
    }

    private static void MapUpdateMe(RouteGroupBuilder group)
    {
        group.MapPatch("/me", async (
            UpdateUserRequest request,
            ClaimsPrincipal principal,
            UpdateMeHandler handler,
            IProblemDetailsService pds,
            HttpContext http,
            CancellationToken ct) =>
        {
            var userId = principal.GetUserId();
            if (userId is null) return Results.Unauthorized();

            var result = await handler.HandleAsync(userId.Value, request, ct);
            return result.IsError
                ? await result.ToProblemDetails(pds, http)
                : Results.Ok(result.Value);
        })
        .WithName("UpdateCurrentUser")
        .WithSummary("Update the logged-in user's profile")
        .RequireAuthorization();
    }

    private static void MapDeleteMe(RouteGroupBuilder group)
    {
        group.MapDelete("/me", async (
            ClaimsPrincipal principal,
            DeleteAccountHandler handler,
            IProblemDetailsService pds,
            HttpContext http,
            CancellationToken ct) =>
        {
            var userId = principal.GetUserId();
            if (userId is null) return Results.Unauthorized();

            var result = await handler.HandleAsync(userId.Value, ct);
            return result.IsError
                ? await result.ToProblemDetails(pds, http)
                : Results.NoContent();
        })
        .WithName("DeleteCurrentUser")
        .WithSummary("Permanently delete the logged-in user's account (irreversible)")
        .RequireAuthorization();
    }

    private static void MapUploadLogo(RouteGroupBuilder group)
    {
        group.MapPost("/me/logo", async (
            IFormFile file,
            ClaimsPrincipal principal,
            UploadLogoHandler handler,
            LinkGenerator links,
            IProblemDetailsService pds,
            HttpContext http,
            CancellationToken ct) =>
        {
            var userId = principal.GetUserId();
            if (userId is null) return Results.Unauthorized();

            if (file is null || file.Length == 0)
                return Results.Problem(statusCode: 422, title: "logo.missing",
                    detail: "No file was uploaded.");

            // Reject on the declared part size before reading the body into memory; the
            // image processor is the authority on the content, this just caps the volume.
            if (file.Length > MaxLogoBytes)
                return Results.Problem(statusCode: 422, title: "logo.too_large",
                    detail: "The uploaded file exceeds the 5 MB limit.");

            // A fresh id per upload → a new retrieval URL → browser/PDF caches can't mask
            // the re-upload. Build the absolute URL now so we can persist it as LogoUrl.
            var logoId = Guid.NewGuid();
            var logoUrl = links.GetUriByName(http, "GetUserLogo", new { logoId })
                ?? $"{http.Request.Scheme}://{http.Request.Host}/auth/logo/{logoId}";

            using var buffer = new MemoryStream();
            await file.CopyToAsync(buffer, ct);

            var result = await handler.HandleAsync(userId.Value, logoId, logoUrl, buffer.ToArray(), ct);
            return result.IsError
                ? await result.ToProblemDetails(pds, http)
                : Results.Ok(result.Value);
        })
        .DisableAntiforgery() // no antiforgery middleware in this API; auth is bearer-token
        .WithName("UploadUserLogo")
        .WithSummary("Upload a business logo image (stored server-side; sets logo_url)")
        .RequireAuthorization();
    }

    private static void MapDeleteLogo(RouteGroupBuilder group)
    {
        group.MapDelete("/me/logo", async (
            ClaimsPrincipal principal,
            DeleteLogoHandler handler,
            IProblemDetailsService pds,
            HttpContext http,
            CancellationToken ct) =>
        {
            var userId = principal.GetUserId();
            if (userId is null) return Results.Unauthorized();

            var result = await handler.HandleAsync(userId.Value, ct);
            return result.IsError
                ? await result.ToProblemDetails(pds, http)
                : Results.Ok(result.Value);
        })
        .WithName("DeleteUserLogo")
        .WithSummary("Clear the logged-in user's uploaded logo")
        .RequireAuthorization();
    }

    private static void MapGetLogo(IEndpointRouteBuilder app)
    {
        app.MapGet("/auth/logo/{logoId:guid}", async (
            Guid logoId,
            GetLogoHandler handler,
            CancellationToken ct) =>
        {
            var result = await handler.HandleAsync(logoId, ct);
            // Plain 404 (not ProblemDetails) — the consumer is an <img>/PDF loader.
            return result.IsError
                ? Results.NotFound()
                : Results.File(result.Value.Bytes, result.Value.ContentType);
        })
        .AllowAnonymous()
        .WithTags("auth")
        .WithName("GetUserLogo")
        .WithSummary("Fetch a stored logo image by its opaque id (public)");
    }
}
