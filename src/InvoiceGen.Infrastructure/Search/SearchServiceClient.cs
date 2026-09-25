using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace InvoiceGen.Infrastructure.Search;

// Typed HttpClient over the external Python InvoiceGen.Search service. BaseAddress is configured
// from Search:BaseUrl in DI (see DependencyInjection). The caller's bearer token is forwarded
// per-request as Authorization: Bearer <token>, so the search service authenticates as the same
// user that made the incoming API request — we set it on the HttpRequestMessage rather than on
// HttpClient.DefaultRequestHeaders because the typed client instance is shared across requests.
public sealed class SearchServiceClient(HttpClient httpClient)
{
    // The Python service speaks snake_case (query, customer_id, min_amount, ...). The app's global
    // ASP.NET JSON options don't apply to HttpClient serialization, so the wire contract with the
    // search service is configured explicitly here. Null filters are omitted rather than sent as
    // null so the request carries only the filters the caller actually supplied.
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) }
    };

    public async Task<SearchResponse?> SearchAsync(
        SearchRequest request,
        string bearerToken,
        CancellationToken cancellationToken)
    {
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/search")
        {
            Content = JsonContent.Create(request, options: JsonOptions)
        };
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);

        using var response = await httpClient.SendAsync(httpRequest, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<SearchResponse>(JsonOptions, cancellationToken);
    }
}

// Request/response shapes for POST /search — mirror the Python service's contract. Query is
// required; every filter is optional and omitted from the wire request when null. Money bounds are
// decimal (never double) per the repo's money convention; the date range filters on document date.
public sealed record SearchRequest(
    string Query,
    Guid? CustomerId = null,
    string? Status = null,
    decimal? MinAmount = null,
    decimal? MaxAmount = null,
    DateOnly? DateFrom = null,
    DateOnly? DateTo = null,
    int Limit = 10);

public sealed record SearchResponse(IReadOnlyList<SearchResult> Results);

public sealed record SearchResult(Guid DocumentId, double Score);
