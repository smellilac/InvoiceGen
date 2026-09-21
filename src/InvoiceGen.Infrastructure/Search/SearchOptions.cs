namespace InvoiceGen.Infrastructure.Search;

// Connection settings for the external Python InvoiceGen.Search service.
// BaseUrl points at that service's root; the client posts to /search relative to it.
public sealed class SearchOptions
{
    public const string SectionName = "Search";

    public string BaseUrl { get; set; } = "http://localhost:8000";
}
