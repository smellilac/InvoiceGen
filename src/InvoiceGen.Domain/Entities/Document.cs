using InvoiceGen.Domain.Enums;

namespace InvoiceGen.Domain.Entities;

public class Document
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    // Optional reference to a saved Customer, kept for filtering/lookup only.
    // `To` is a frozen snapshot taken at creation — editing the customer never rewrites it.
    public Guid? CustomerId { get; set; }
    public DocumentType Type { get; set; }
    public DocumentStatus Status { get; set; }

    public string? Number { get; set; }
    // Free-text, never validated (same policy as Number) — e.g. a credit_note pointing
    // at the invoice it credits. See openapi related_document_number.
    public string? RelatedDocumentNumber { get; set; }
    public string From { get; set; } = null!;
    public string To { get; set; } = null!;
    public string Currency { get; set; } = "USD";

    // A FROZEN snapshot of the user's profile logo URL, captured at creation (same policy as
    // `To`/`From`: not a live link to the profile). Copied from AppUser.LogoUrl when the create
    // request opts in (include_logo) and a profile logo is set; otherwise null. The PDF renderer
    // reads THIS field, never the current profile — editing/removing the profile logo later never
    // rewrites it. See openapi x-customer-policy.
    public string? LogoUrl { get; set; }

    public DateOnly Date { get; set; }
    public DateOnly? DueDate { get; set; }

    // Inputs to the totals (kept because the PDF needs them; not all are exposed in the API response).
    public decimal TaxPercent { get; set; }
    public decimal DiscountPercent { get; set; }
    public decimal ShippingAmount { get; set; }
    // Meaning depends on Type (x-settlement-policy): money received for money-owed types,
    // money refunded for credit_note.
    public decimal AmountSettled { get; set; }

    // Computed + stored so the saved document always shows the totals it was created with.
    public decimal Subtotal { get; private set; }        // pre-discount
    public decimal DiscountAmount { get; private set; }  // total discount (subtotal - discounted)
    public decimal TaxAmount { get; private set; }       // total tax (sum of per-line tax)
    public decimal Total { get; private set; }
    public decimal BalanceRemaining { get; private set; }

    public string? Notes { get; set; }
    public string? Terms { get; set; }

    // Email send tracking (x-email-delivery-policy). SendCount counts attempts (incremented
    // at enqueue); LastSentAt/LastSendStatus/LastSendError are updated by the worker.
    public DateTimeOffset? LastSentAt { get; private set; }
    public int SendCount { get; private set; }
    public SendStatus? LastSendStatus { get; private set; }
    public string? LastSendError { get; private set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    // SOFT delete (same policy as Customer.DeletedAt): a deleted document is hidden from
    // list/get/pdf/send via a global query filter, but the row is kept for history.
    public DateTimeOffset? DeletedAt { get; set; }

    public bool IsDeleted => DeletedAt is not null;

    public List<LineItem> Items { get; set; } = [];

    // Money rules — see openapi x-rounding-policy (tax_rounding, discount_application):
    // everything is PER LINE ITEM, never once on a lump sum. For each line: discount it and
    // round, then tax that discounted amount and round. `Subtotal` stays PRE-discount (the
    // raw sum of line totals). All money uses decimal + round-half-away-from-zero.
    public void Recalculate()
    {
        var discountFactor = 1m - DiscountPercent / 100m;

        decimal subtotal = 0m;         // pre-discount: raw sum of line totals
        decimal discountedTotal = 0m;  // sum of per-line discounted amounts
        decimal taxTotal = 0m;         // sum of per-line tax on the discounted amount

        foreach (var item in Items)
        {
            var lineTotal = item.LineTotal;
            subtotal += lineTotal;

            var discounted = Round(lineTotal * discountFactor);
            discountedTotal += discounted;
            taxTotal += Round(discounted * TaxPercent / 100m);
        }

        Subtotal = Round(subtotal);
        DiscountAmount = Round(Subtotal - discountedTotal); // total discount applied
        TaxAmount = Round(taxTotal);                        // total tax (sum of per-line)
        Total = Round(discountedTotal + taxTotal + ShippingAmount);
        BalanceRemaining = Round(Total - AmountSettled);
    }

    private static decimal Round(decimal value) => decimal.Round(value, 2, MidpointRounding.AwayFromZero);

    // --- Email send state transitions (x-email-delivery-policy) ---

    // At enqueue: count the attempt and mark queued. LastSentAt is NOT touched here.
    public void MarkSendEnqueued(DateTimeOffset now)
    {
        SendCount++;
        LastSendStatus = SendStatus.Queued;
        UpdatedAt = now;
    }

    // Worker, on a successful send: record when it actually reached the customer.
    public void MarkSent(DateTimeOffset now)
    {
        LastSentAt = now;
        LastSendStatus = SendStatus.Sent;
        LastSendError = null;
        UpdatedAt = now;
    }

    // Worker, on failure: keep LastSentAt (last real delivery) untouched; surface the reason.
    public void MarkSendFailed(string error, DateTimeOffset now)
    {
        LastSendStatus = SendStatus.Failed;
        LastSendError = error;
        UpdatedAt = now;
    }
}
