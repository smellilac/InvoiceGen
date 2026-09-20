using InvoiceGen.Application.Features.Documents;
using InvoiceGen.Domain.Enums;

namespace InvoiceGen.Application.Features.DocumentTypes;

public static class DocumentTypesExtensions
{
    public static DocumentTypeDto[] GetAll() =>
    [
        Build(DocumentType.Invoice,         "Bill a customer for products or services."),
        Build(DocumentType.Receipt,         "Acknowledge payment received."),
        Build(DocumentType.CreditNote,      "Issue a refund or credit against a previous invoice."),
        Build(DocumentType.Quote,           "Provide a price estimate before work begins."),
        Build(DocumentType.Estimate,        "Give a rough cost estimate for a project."),
        Build(DocumentType.ProformaInvoice, "A preliminary invoice sent before goods are delivered."),
        Build(DocumentType.PurchaseOrder,   "Authorize a purchase from a supplier."),
        Build(DocumentType.SalesOrder,      "Confirm a sale of goods or services to a customer."),
        Build(DocumentType.Statement,       "Summarize outstanding invoices for a customer."),
        Build(DocumentType.Timesheet,       "Bill for time worked on a project."),
        Build(DocumentType.WorkOrder,       "Authorize and track a specific job or task."),
        Build(DocumentType.PackingSlip,     "List items included in a shipment."),
    ];

    private static DocumentTypeDto Build(DocumentType type, string description) =>
        new(
            DocumentTypeApi.ToApi(type),
            DocumentTypeApi.ToDisplayName(type),
            description,
            DocumentTypeApi.ToApi(type));
}