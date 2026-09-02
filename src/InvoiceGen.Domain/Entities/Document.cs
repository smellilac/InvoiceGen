using InvoiceGen.Domain.Enums;

namespace InvoiceGen.Domain.Entities;

public class Document
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public DocumentType Type { get; set; }
    public DocumentStatus Status { get; set; }

    public string? Number { get; set; }
    // Free-text, never validated (same policy as Number) — e.g. a credit_note pointing
    // at the invoice it credits. See openapi related_document_number.
    public string? RelatedDocumentNumber { get; set; }
    public string From { get; set; } = null!;
    public string To { get; set; } = null!;
    public string Currency { get; set; } = "USD";

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
    public decimal Subtotal { get; private set; }
    public decimal Total { get; private set; }
    public decimal BalanceRemaining { get; private set; }

    public string? Notes { get; set; }
    public string? Terms { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

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
        Total = Round(discountedTotal + taxTotal + ShippingAmount);
        BalanceRemaining = Round(Total - AmountSettled);
    }

    private static decimal Round(decimal value) => decimal.Round(value, 2, MidpointRounding.AwayFromZero);
}
