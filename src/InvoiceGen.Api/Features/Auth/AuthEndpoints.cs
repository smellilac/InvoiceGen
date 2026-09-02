using System.Security.Claims;
using InvoiceGen.Api.Common;
using InvoiceGen.Application.Features.Auth;

namespace InvoiceGen.Api.Features.Auth;

public static class AuthEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/auth").WithTags("auth").RequireRateLimiting("auth");

        MapRegister(group);
        MapLogin(group);
        MapRefresh(group);
        MapLogout(group);
        MapGetMe(group);
        MapUpdateMe(group);
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
}
