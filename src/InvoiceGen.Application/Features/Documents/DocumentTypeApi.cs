using System.Text.Json;
using InvoiceGen.Domain.Enums;

namespace InvoiceGen.Application.Features.Documents;

// Maps DocumentType <-> the snake_case string used on the wire (e.g. CreditNote <-> "credit_note").
// Used for the ?type= query filter; the request/response body uses the JSON enum converter.
public static class DocumentTypeApi
{
    public static string ToApi(DocumentType type) =>
        JsonNamingPolicy.SnakeCaseLower.ConvertName(type.ToString());

    // Single source of truth for human-readable type names (used by the /document-types
    // listing and the PDF title). Explicit so future irregular names stay under control.
    public static string ToDisplayName(DocumentType type) => type switch
    {
        DocumentType.Invoice => "Invoice",
        DocumentType.Receipt => "Receipt",
        DocumentType.CreditNote => "Credit Note",
        DocumentType.Quote => "Quote",
        DocumentType.Estimate => "Estimate",
        DocumentType.ProformaInvoice => "Proforma Invoice",
        DocumentType.PurchaseOrder => "Purchase Order",
        DocumentType.Statement => "Statement",
        DocumentType.Timesheet => "Timesheet",
        DocumentType.WorkOrder => "Work Order",
        DocumentType.PackingSlip => "Packing Slip",
        _ => type.ToString()
    };

    public static bool TryParse(string? value, out DocumentType type)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            foreach (var candidate in Enum.GetValues<DocumentType>())
            {
                if (ToApi(candidate).Equals(value, StringComparison.OrdinalIgnoreCase))
                {
                    type = candidate;
                    return true;
                }
            }
        }

        type = default;
        return false;
    }
}
