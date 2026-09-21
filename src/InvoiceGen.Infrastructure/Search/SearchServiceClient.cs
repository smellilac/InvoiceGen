using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace InvoiceGen.Infrastructure.Search;

// Typed HttpClient over the external Python InvoiceGen.Search service. BaseAddress is configured
// from Search:BaseUrl in DI (see DependencyInjection). The caller's bearer token is forwarded
// per-request as Authorization: Bearer <token>, so the search service authenticates as the same
// user that made the incoming API request — we set it on the HttpRequestMessage rather than on
// HttpClient.DefaultRequestHeaders because the typed client instance is shared across requests.
public sealed class SearchServiceClient(HttpClient httpClient)
{
    public async Task<SearchResponse?> SearchAsync(
        SearchRequest request,
        string bearerToken,
        CancellationToken cancellationToken)
    {
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/search")
        {
            Content = JsonContent.Create(request)
        };
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);

        using var response = await httpClient.SendAsync(httpRequest, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<SearchResponse>(cancellationToken);
    }
}

// Request/response shapes for POST /search. Kept intentionally small — expand alongside the
// Python service's contract as search features are wired up.
public sealed record SearchRequest(string Query, int Limit = 10);

public sealed record SearchResponse(IReadOnlyList<SearchResult> Results);

public sealed record SearchResult(Guid DocumentId, double Score);
