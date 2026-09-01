using ErrorOr;

namespace InvoiceGen.Application.Features.DocumentTypes;

// TODO if types will be static - remove handler - it is just wrapper
// it there are will be call to DB to get types or some logic - leave this
public sealed class ListDocumentTypesHandler
{
    private static readonly DocumentTypeDto[] Types =
    [
        new("invoice",          "Invoice",          "Bill a customer for products or services.",                        "invoice"),
        new("receipt",          "Receipt",          "Acknowledge payment received.",                                    "receipt"),
        new("credit_note",      "Credit Note",      "Issue a refund or credit against a previous invoice.",             "credit_note"),
        new("quote",            "Quote",            "Provide a price estimate before work begins.",                     "quote"),
        new("estimate",         "Estimate",         "Give a rough cost estimate for a project.",                        "estimate"),
        new("proforma_invoice", "Proforma Invoice", "A preliminary invoice sent before goods are delivered.",           "proforma_invoice"),
        new("purchase_order",   "Purchase Order",   "Authorize a purchase from a supplier.",                           "purchase_order"),
        new("statement",        "Statement",        "Summarize outstanding invoices for a customer.",                   "statement"),
        new("timesheet",        "Timesheet",        "Bill for time worked on a project.",                               "timesheet"),
        new("work_order",       "Work Order",       "Authorize and track a specific job or task.",                      "work_order"),
        new("packing_slip",     "Packing Slip",     "List items included in a shipment.",                               "packing_slip"),
    ];

    public Task<ErrorOr<DocumentTypeDto[]>> HandleAsync(CancellationToken cancellationToken)
    {
        ErrorOr<DocumentTypeDto[]> result = Types;
        return Task.FromResult(result);
    }
}
