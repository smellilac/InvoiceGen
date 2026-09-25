using System.Net;
using System.Net.Http.Headers;
using InvoiceGen.Infrastructure.Search;

namespace InvoiceGen.Api.Features.Search;

// Thin proxy in front of the external Python InvoiceGen.Search service. The endpoint forwards the
// caller's request and bearer token to the service (so it authenticates as the same user) and
// relays the results, translating transport-level failures into meaningful status codes.
public static class SearchEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/search").WithTags("search").RequireAuthorization();

        MapSearch(group);
    }

    private static void MapSearch(RouteGroupBuilder group)
    {
        group.MapPost("/", async (
            SearchRequest request,
            SearchServiceClient searchClient,
            HttpContext http,
            CancellationToken ct) =>
        {
            // The group requires auth, so a valid bearer token is present. Forward it verbatim so
            // the search service authorizes as the same user (scoping results to their documents).
            var token = GetBearerToken(http.Request);
            if (token is null) return Results.Unauthorized();

            try
            {
                var response = await searchClient.SearchAsync(request, token, ct);
                return Results.Ok(response);
            }
            catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
            {
                // The search service rejected the token — pass the 401 straight through.
                return Results.Unauthorized();
            }
            catch (HttpRequestException ex) when (ex.StatusCode is null)
            {
                // No HTTP response at all (connection refused, DNS failure, etc.) — the service is
                // unreachable rather than misbehaving.
                return Results.Problem(
                    statusCode: StatusCodes.Status503ServiceUnavailable,
                    title: "search_unavailable",
                    detail: "The search service is currently unavailable. Please try again later.");
            }
            catch (HttpRequestException)
            {
                // The service responded, but with a non-success status (500, 400, ...). It's a bad
                // gateway from the caller's point of view.
                return Results.Problem(
                    statusCode: StatusCodes.Status502BadGateway,
                    title: "search_failed",
                    detail: "The search service returned an unexpected error.");
            }
            catch (TaskCanceledException) when (!ct.IsCancellationRequested)
            {
                // Our request to the service timed out (the caller didn't cancel) — treat as
                // unavailable.
                return Results.Problem(
                    statusCode: StatusCodes.Status503ServiceUnavailable,
                    title: "search_unavailable",
                    detail: "The search service did not respond in time. Please try again later.");
            }
        })
        .WithName("Search")
        .WithSummary("Search the current user's documents via the InvoiceGen.Search service");
    }

    private static string? GetBearerToken(HttpRequest request)
    {
        if (!AuthenticationHeaderValue.TryParse(request.Headers.Authorization, out var header) ||
            !string.Equals(header.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(header.Parameter))
        {
            return null;
        }

        return header.Parameter;
    }
}
