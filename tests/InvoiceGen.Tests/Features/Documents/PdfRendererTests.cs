using InvoiceGen.Domain.Entities;
using InvoiceGen.Domain.Enums;
using InvoiceGen.Infrastructure.Pdf;
using UglyToad.PdfPig;

namespace InvoiceGen.Tests.Features.Documents;

// Renders a Document directly and reads the PDF text back with PdfPig — no DB/host, so
// these run without Docker (QuestPDF renders fine on this Linux env). Closes the gap where
// the packing-slip "no pricing" and credit-note "Refunded" rules were code-only, untested.
public class PdfRendererTests
{
    private static byte[] Render(DocumentType type)
    {
        var document = new Document
        {
            Type = type,
            From = "My Company",
            To = "The Customer",
            Currency = "USD",
            Date = new DateOnly(2026, 1, 15),
            Items = [new LineItem { Name = "PackWidget", Quantity = 2m, UnitCost = 77.77m }]
        };
        document.Recalculate();
        return new PdfRenderer().Render(document);
    }

    // Whitespace-stripped text, so assertions don't depend on PDF glyph spacing.
    private static string FlatText(byte[] pdf)
    {
        using var doc = PdfDocument.Open(pdf);
        var text = string.Concat(doc.GetPages().Select(p => p.Text));
        return new string(text.Where(ch => !char.IsWhiteSpace(ch)).ToArray());
    }

    [Fact]
    public void PackingSlip_ListsItems_ButHidesPricing()
    {
        var text = FlatText(Render(DocumentType.PackingSlip));

        Assert.Contains("PackWidget", text);      // items are still listed
        Assert.DoesNotContain("77.77", text);      // ...but no unit price
        Assert.DoesNotContain("Subtotal", text);   // ...and no totals block
        Assert.DoesNotContain("Total", text);
        Assert.DoesNotContain("Balance", text);
        Assert.DoesNotContain("Paid", text);
    }

    [Fact]
    public void CreditNote_ShowsRefunded_NotPaid()
    {
        var text = FlatText(Render(DocumentType.CreditNote));

        Assert.Contains("Refunded", text);          // settlement label for a credit note
        Assert.DoesNotContain("Paid", text);
    }

    [Fact]
    public void Invoice_ShowsPricingAndPaid_NotRefunded()
    {
        var text = FlatText(Render(DocumentType.Invoice));

        Assert.Contains("Subtotal", text);
        Assert.Contains("77.77", text);             // priced document
        Assert.Contains("Paid", text);
        Assert.DoesNotContain("Refunded", text);
    }
}
