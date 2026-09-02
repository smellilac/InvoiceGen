using InvoiceGen.Application.Features.Documents;
using InvoiceGen.Domain.Enums;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using DomainDocument = InvoiceGen.Domain.Entities.Document;

namespace InvoiceGen.Infrastructure.Pdf;

// TODO maybe should be changed to Strategy with different PdfRenderers
public sealed class PdfRenderer : IPdfRenderer
{
    static PdfRenderer() => QuestPDF.Settings.License = LicenseType.Community;

    public byte[] Render(DomainDocument document)
    {
        // A packing slip is a shipping document — by convention it never shows prices
        // or totals (it can travel to someone who isn't paying). Same layout otherwise.
        var isPackingSlip = document.Type == DocumentType.PackingSlip;

        return QuestPDF.Fluent.Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(40);
                page.DefaultTextStyle(x => x.FontSize(10));

                page.Header().Row(row =>
                {
                    row.RelativeItem().Column(col =>
                    {
                        col.Item().Text(TitleFor(document)).FontSize(20).Bold();
                        if (!string.IsNullOrWhiteSpace(document.Number))
                            col.Item().Text($"No. {document.Number}");
                    });
                    row.ConstantItem(160).Column(col =>
                    {
                        col.Item().AlignRight().Text($"Date: {document.Date:yyyy-MM-dd}");
                        if (document.DueDate is { } due)
                            col.Item().AlignRight().Text($"Due: {due:yyyy-MM-dd}");
                    });
                });

                page.Content().PaddingVertical(15).Column(col =>
                {
                    col.Spacing(10);

                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text("From").Bold();
                            c.Item().Text(document.From);
                        });
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text("To").Bold();
                            c.Item().Text(document.To);
                        });
                    });

                    if (isPackingSlip)
                        BuildPackingSlipTable(col, document);
                    else
                        BuildPricedTable(col, document);

                    // Totals never appear on a packing slip.
                    if (!isPackingSlip)
                    {
                        col.Item().AlignRight().Column(totals =>
                        {
                            totals.Item().Text($"Subtotal: {Money(document.Currency, document.Subtotal)}");
                            if (document.TaxPercent > 0)
                                totals.Item().Text($"Tax: {document.TaxPercent:N2}%");
                            totals.Item().Text($"Total: {Money(document.Currency, document.Total)}").Bold();
                            totals.Item().Text($"Paid: {Money(document.Currency, document.AmountPaid)}");
                            totals.Item().Text($"Balance due: {Money(document.Currency, document.BalanceDue)}").Bold();
                        });
                    }

                    if (!string.IsNullOrWhiteSpace(document.Notes))
                        col.Item().Text($"Notes: {document.Notes}");
                    if (!string.IsNullOrWhiteSpace(document.Terms))
                        col.Item().Text($"Terms: {document.Terms}");
                });
            });
        }).GeneratePdf();
    }

    private static void BuildPricedTable(ColumnDescriptor col, DomainDocument document)
    {
        col.Item().Table(table =>
        {
            table.ColumnsDefinition(c =>
            {
                c.RelativeColumn(5);
                c.RelativeColumn(1);
                c.RelativeColumn(2);
                c.RelativeColumn(2);
            });

            table.Header(header =>
            {
                header.Cell().Text("Item").Bold();
                header.Cell().AlignRight().Text("Qty").Bold();
                header.Cell().AlignRight().Text("Unit").Bold();
                header.Cell().AlignRight().Text("Total").Bold();
            });

            foreach (var item in document.Items)
            {
                table.Cell().Text(item.Name);
                table.Cell().AlignRight().Text(MoneyFormatter.Quantity(item.Quantity));
                table.Cell().AlignRight().Text(Money(document.Currency, item.UnitCost));
                table.Cell().AlignRight().Text(Money(document.Currency, item.LineTotal));
            }
        });
    }

    // Packing slip: Item, Description, Quantity — no prices.
    private static void BuildPackingSlipTable(ColumnDescriptor col, DomainDocument document)
    {
        col.Item().Table(table =>
        {
            table.ColumnsDefinition(c =>
            {
                c.RelativeColumn(4);
                c.RelativeColumn(4);
                c.RelativeColumn(1);
            });

            table.Header(header =>
            {
                header.Cell().Text("Item").Bold();
                header.Cell().Text("Description").Bold();
                header.Cell().AlignRight().Text("Qty").Bold();
            });

            foreach (var item in document.Items)
            {
                table.Cell().Text(item.Name);
                table.Cell().Text(item.Description ?? string.Empty);
                table.Cell().AlignRight().Text(MoneyFormatter.Quantity(item.Quantity));
            }
        });
    }

    private static string TitleFor(DomainDocument document) =>
        DocumentTypeApi.ToDisplayName(document.Type); // "Credit Note", not "CreditNote"

    private static string Money(string currency, decimal amount) => MoneyFormatter.Format(currency, amount);
}
