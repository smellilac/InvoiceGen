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
    // PDF palette — deliberately neutral (dark navy accent), NOT the app's brand blue.
    // The generated document should read as professional to recipients, a different
    // context than the web UI, which keeps its own #3F51B5 branding. The accent drives
    // the title, the rule under it, the table header background, and the total figure.
    private const string Accent = "#475569";     // medium slate gray-blue — accent
    private const string TotalTint = "#EEF1F5";  // neutral light gray — highlighted total box
    private const string LabelGray = "#6B7280";  // muted gray — field labels
    private const string ValueBlack = "#000000"; // values (names, amounts)
    private const string RowBorder = "#E5E7EB";  // hairline between table rows
    private const string White = "#FFFFFF";

    // Relative to font size; ~0.05em of extra tracking on labels.
    private const float LabelSpacing = 0.05f;

    static PdfRenderer() => QuestPDF.Settings.License = LicenseType.Community;

    // Cap the logo to a modest band so it can never crowd out or obscure the legally required
    // header fields (seller info, document number, dates) or the totals below. A tall/large logo
    // is scaled down to fit this box; it is never allowed to dominate the page.
    private const float LogoMaxHeight = 56f;
    private const float LogoMaxWidth = 200f;

    public byte[] Render(DomainDocument document, byte[]? logo = null) =>
        Compose(document, logo).GeneratePdf();

    private static IDocument Compose(DomainDocument document, byte[]? logo)
    {
        // A packing slip is a shipping document — by convention it never shows prices
        // or totals (it can travel to someone who isn't paying). Same layout otherwise.
        var isPackingSlip = document.Type == DocumentType.PackingSlip;

        // x-settlement-policy: the shared settled/remaining fields mean "refunded" for a
        // credit note, "paid/owed" for the money-owed-to-you types.
        var isCreditNote = document.Type == DocumentType.CreditNote;
        var settledLabel = isCreditNote ? "Refunded" : "Paid";
        var remainingLabel = isCreditNote ? "Balance remaining" : "Balance due";

        return QuestPDF.Fluent.Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(40);
                page.DefaultTextStyle(x => x.FontSize(10).FontColor(ValueBlack));

                page.Header().Column(header =>
                {
                    // Frozen per-document logo (see Document.LogoUrl). Sits above the title band in
                    // its own capped box, so it adds branding without displacing the seller/number/
                    // date fields that must stay visible.
                    if (logo is { Length: > 0 })
                        header.Item().PaddingBottom(8).AlignLeft()
                            .MaxHeight(LogoMaxHeight).MaxWidth(LogoMaxWidth)
                            .Image(logo).FitArea();

                    header.Item().Row(row =>
                    {
                        row.RelativeItem().Column(col =>
                        {
                            // Document-type title: the most prominent element on the page.
                            col.Item().Text(TitleFor(document)).FontSize(28).Bold().FontColor(Accent);
                            if (!string.IsNullOrWhiteSpace(document.Number))
                                col.Item().PaddingTop(2).Text($"No. {document.Number}")
                                    .FontSize(9).FontColor(LabelGray);
                            if (!string.IsNullOrWhiteSpace(document.RelatedDocumentNumber))
                                col.Item().Text($"Re: {document.RelatedDocumentNumber}")
                                    .FontSize(9).FontColor(LabelGray);
                        });
                        row.ConstantItem(170).Column(col =>
                        {
                            col.Item().AlignRight().Text(text =>
                            {
                                text.Span("DATE  ").FontColor(LabelGray).LetterSpacing(LabelSpacing);
                                text.Span($"{document.Date:yyyy-MM-dd}").Bold().FontColor(ValueBlack);
                            });
                            if (document.DueDate is { } due)
                                col.Item().AlignRight().Text(text =>
                                {
                                    text.Span("DUE  ").FontColor(LabelGray).LetterSpacing(LabelSpacing);
                                    text.Span($"{due:yyyy-MM-dd}").Bold().FontColor(ValueBlack);
                                });
                        });
                    });

                    // Accent rule under the header ties the title/company block together.
                    header.Item().PaddingTop(10).LineHorizontal(2).LineColor(Accent);
                });

                page.Content().PaddingVertical(15).Column(col =>
                {
                    col.Spacing(12);

                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Column(c => Field(c, "FROM", document.From));
                        row.RelativeItem().Column(c => Field(c, "TO", document.To));
                    });

                    if (isPackingSlip)
                        BuildPackingSlipTable(col, document);
                    else
                        BuildPricedTable(col, document);

                    // Totals never appear on a packing slip.
                    if (!isPackingSlip)
                    {
                        col.Item().Row(row =>
                        {
                            row.RelativeItem(); // spacer — pushes the totals block to the right
                            row.ConstantItem(240).Column(totals =>
                            {
                                totals.Spacing(4);

                                TotalLine(totals, "Subtotal", Money(document.Currency, document.Subtotal));
                                if (document.TaxPercent > 0)
                                    TotalLine(totals, "Tax", $"{document.TaxPercent:N2}%");

                                // Headline figure in a light-blue box so it stands out from
                                // Subtotal/Paid above and below it.
                                totals.Item().Background(TotalTint).Padding(8).Row(box =>
                                {
                                    box.RelativeItem().AlignMiddle().Text("Total")
                                        .FontColor(LabelGray).LetterSpacing(LabelSpacing).Bold();
                                    box.AutoItem().AlignMiddle().Text(Money(document.Currency, document.Total))
                                        .Bold().FontSize(14).FontColor(Accent);
                                });

                                TotalLine(totals, settledLabel, Money(document.Currency, document.AmountSettled));
                                TotalLine(totals, remainingLabel, Money(document.Currency, document.BalanceRemaining),
                                    boldValue: true);
                            });
                        });
                    }

                    if (!string.IsNullOrWhiteSpace(document.Notes))
                    {
                        col.Item().Text("NOTES").FontColor(LabelGray).LetterSpacing(LabelSpacing).Bold();
                        col.Item().Text(document.Notes).FontColor(ValueBlack);
                    }
                    if (!string.IsNullOrWhiteSpace(document.Terms))
                    {
                        col.Item().Text("TERMS").FontColor(LabelGray).LetterSpacing(LabelSpacing).Bold();
                        col.Item().Text(document.Terms).FontColor(ValueBlack);
                    }
                });
            });
        });
    }

    // A stacked field: muted, letter-spaced label above a bold black value (which may be
    // multiline, e.g. a company name + address).
    private static void Field(ColumnDescriptor col, string label, string value)
    {
        col.Item().Text(label).FontSize(8).FontColor(LabelGray).LetterSpacing(LabelSpacing).Bold();
        col.Item().Text(value).Bold().FontColor(ValueBlack);
    }

    // A right-aligned totals line: gray label on the left, value on the right.
    private static void TotalLine(ColumnDescriptor col, string label, string value, bool boldValue = false)
    {
        col.Item().Row(row =>
        {
            row.RelativeItem().Text(label).FontColor(LabelGray).LetterSpacing(LabelSpacing);
            var span = row.AutoItem().Text(value).FontColor(ValueBlack);
            if (boldValue)
                span.Bold();
        });
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
                HeaderCell(header.Cell(), "Item");
                HeaderCell(header.Cell(), "Qty", alignRight: true);
                HeaderCell(header.Cell(), "Unit", alignRight: true);
                HeaderCell(header.Cell(), "Total", alignRight: true);
            });

            foreach (var item in document.Items)
            {
                BodyCell(table.Cell()).Text(item.Name).Bold().FontColor(ValueBlack);
                BodyCell(table.Cell()).AlignRight().Text(MoneyFormatter.Quantity(item.Quantity)).FontColor(ValueBlack);
                BodyCell(table.Cell()).AlignRight().Text(Money(document.Currency, item.UnitCost)).FontColor(ValueBlack);
                BodyCell(table.Cell()).AlignRight().Text(Money(document.Currency, item.LineTotal)).Bold().FontColor(ValueBlack);
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
                HeaderCell(header.Cell(), "Item");
                HeaderCell(header.Cell(), "Description");
                HeaderCell(header.Cell(), "Qty", alignRight: true);
            });

            foreach (var item in document.Items)
            {
                BodyCell(table.Cell()).Text(item.Name).Bold().FontColor(ValueBlack);
                BodyCell(table.Cell()).Text(item.Description ?? string.Empty).FontColor(ValueBlack);
                BodyCell(table.Cell()).AlignRight().Text(MoneyFormatter.Quantity(item.Quantity)).FontColor(ValueBlack);
            }
        });
    }

    // Solid brand-blue header cell with white bold text.
    private static void HeaderCell(IContainer cell, string text, bool alignRight = false)
    {
        cell = cell.Background(Accent).PaddingVertical(6).PaddingHorizontal(6);
        if (alignRight)
            cell = cell.AlignRight();
        cell.Text(text).FontColor(White).Bold();
    }

    // Body cell with padding and a hairline bottom border for readability.
    private static IContainer BodyCell(IContainer cell) =>
        cell.BorderBottom(0.5f).BorderColor(RowBorder).PaddingVertical(4).PaddingHorizontal(6);

    private static string TitleFor(DomainDocument document) =>
        DocumentTypeApi.ToDisplayName(document.Type); // "Credit Note", not "CreditNote"

    private static string Money(string currency, decimal amount) => MoneyFormatter.Format(currency, amount);
}
