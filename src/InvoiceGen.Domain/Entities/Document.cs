using InvoiceGen.Domain.Enums;

namespace InvoiceGen.Domain.Entities;

public class Document
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public DocumentType Type { get; set; }
    public DocumentStatus Status { get; set; }

    public string? Number { get; set; }
    public string From { get; set; } = null!;
    public string To { get; set; } = null!;
    public string Currency { get; set; } = "USD";

    public DateOnly Date { get; set; }
    public DateOnly? DueDate { get; set; }

    // Inputs to the totals (kept because the PDF needs them; not all are exposed in the API response).
    public decimal TaxPercent { get; set; }
    public decimal DiscountPercent { get; set; }
    public decimal ShippingAmount { get; set; }
    public decimal AmountPaid { get; set; }

    // Computed + stored so the saved document always shows the totals it was created with.
    public decimal Subtotal { get; private set; }
    public decimal Total { get; private set; }
    public decimal BalanceDue { get; private set; }

    public string? Notes { get; set; }
    public string? Terms { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public List<LineItem> Items { get; set; } = [];

    // Money rules (see docs/conventions.md): tax is rounded PER LINE ITEM then summed,
    // never once on the subtotal. All money uses decimal + round-half-away-from-zero.
    public void Recalculate()
    {
        decimal subtotal = 0m;
        decimal taxTotal = 0m;

        foreach (var item in Items)
        {
            var lineTotal = item.LineTotal;
            subtotal += lineTotal;
            taxTotal += Round(lineTotal * TaxPercent / 100m);
        }

        subtotal = Round(subtotal);
        var discount = Round(subtotal * DiscountPercent / 100m);

        Subtotal = subtotal;
        Total = Round(subtotal - discount + taxTotal + ShippingAmount);
        BalanceDue = Round(Total - AmountPaid);
    }

    private static decimal Round(decimal value) => decimal.Round(value, 2, MidpointRounding.AwayFromZero);
}
