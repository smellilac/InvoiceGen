using InvoiceGen.Domain.Enums;

namespace InvoiceGen.Application.Common;

// Indexes a document in the external search service so it becomes searchable. Called
// fire-and-forget after a document is saved — indexing is BEST-EFFORT: a document is fully
// created and usable whether or not it ever reaches the search service. Implementations talk to
// the (possibly unavailable) Python InvoiceGen.Search service, so callers must treat failures as
// non-fatal and never let them affect the originating request.
public interface ISearchIndexer
{
    Task IndexDocumentAsync(SearchIndexDocument document, CancellationToken cancellationToken);
}

// The projection sent to the search service for indexing: the document's id, its owner (so the
// service can scope search results per user without a bearer token), and the free-text fields
// worth searching over. Money/date fields are omitted — those are structured filters the search
// service can read straight from its own copy or the database, not full-text content.
public sealed record SearchIndexDocument(
    Guid DocumentId,
    Guid UserId,
    DocumentType Type,
    string? Number,
    string From,
    string To,
    string Currency,
    string? Notes,
    string? Terms,
    IReadOnlyList<string> LineItems);
