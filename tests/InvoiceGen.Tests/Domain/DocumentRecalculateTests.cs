using InvoiceGen.Domain.Entities;

namespace InvoiceGen.Tests.Domain;

// Pure domain tests for the money math — no DB/host, so they run without Docker.
public class DocumentRecalculateTests
{
    private static Document Recalculated(
        decimal discountPercent, decimal taxPercent, params (decimal Qty, decimal Unit)[] lines)
    {
        var document = new Document
        {
            From = "x",
            To = "y",
            DiscountPercent = discountPercent,
            TaxPercent = taxPercent,
            Items = lines.Select(l => new LineItem { Name = "item", Quantity = l.Qty, UnitCost = l.Unit }).ToList()
        };
        document.Recalculate();
        return document;
    }

    [Fact]
    public void Recalculate_DiscountedPerLineThenTaxed_MatchesWorkedExample()
    {
        // x-rounding-policy discount_application worked example:
        // lines $3,200 + $1,320 at 5% discount / 21% tax.
        // Line 1: 3040.00 discounted -> 638.40 tax. Line 2: 1254.00 discounted -> 263.34 tax.
        // Subtotal (pre-discount) 4520.00, discount 226.00, tax 901.74, total 5195.74.
        var document = Recalculated(5m, 21m, (1m, 3200m), (1m, 1320m));

        Assert.Equal(4520.00m, document.Subtotal);   // stays PRE-discount
        Assert.Equal(5195.74m, document.Total);       // 4294.00 discounted + 901.74 tax
        Assert.Equal(5195.74m, document.BalanceRemaining);  // nothing settled
    }

    [Fact]
    public void Recalculate_TaxIsRoundedPerLine_NotOnceOnTheSubtotal()
    {
        // Three $0.05 lines at 10%, no discount: each line tax 0.005 -> 0.01, summed 0.03.
        // Rounding once on the 0.15 subtotal would give 0.02 -> total 0.17. Expect 0.18.
        var document = Recalculated(0m, 10m, (1m, 0.05m), (1m, 0.05m), (1m, 0.05m));

        Assert.Equal(0.15m, document.Subtotal);
        Assert.Equal(0.18m, document.Total);
    }

    [Fact]
    public void Recalculate_NoDiscountNoTax_TotalEqualsSubtotal()
    {
        var document = Recalculated(0m, 0m, (2m, 50m));

        Assert.Equal(100m, document.Subtotal);
        Assert.Equal(100m, document.Total);
    }
}
