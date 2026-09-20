using Pgvector;

namespace InvoiceGen.Domain.Entities;

// A saved customer (address book). Owned by a user. SOFT-deleted via DeletedAt
// (see docs/decisions-log.md x-customer-policy): a deleted customer is hidden and
// unusable for new documents, but existing documents keep their frozen `to` snapshot
// and customer_id reference.
public class Customer
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }

    public string Name { get; set; } = null!;
    public string? Email { get; set; }
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public string? Notes { get; set; } // private — never rendered on a document

    // Optional embedding vector for semantic search (pgvector). Null until generated.
    public Vector? Embedding { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }

    public bool IsDeleted => DeletedAt is not null;
}
