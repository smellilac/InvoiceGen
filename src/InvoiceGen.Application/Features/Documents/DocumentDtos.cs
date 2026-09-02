using InvoiceGen.Domain.Entities;
using InvoiceGen.Domain.Enums;

namespace InvoiceGen.Application.Features.Documents;

public sealed record CreateLineItemRequest(
    string Name,
    string? Description,
    decimal Quantity,
    decimal UnitCost,
    string? Reference);

public sealed record CreateDocumentRequest(
    DocumentType Type,
    string? From,
    Guid? CustomerId,
    string? To,
    DateOnly Date,
    DateOnly? DueDate,
    string? Number,
    string? RelatedDocumentNumber,
    string? Currency,
    IReadOnlyList<CreateLineItemRequest> Items,
    decimal TaxPercent,
    decimal DiscountPercent,
    decimal ShippingAmount,
    string? Notes,
    string? Terms,
    decimal AmountSettled);

// Matches the OpenAPI `Document` schema — note it intentionally does NOT echo the
// line items or the tax/discount inputs; those are stored for the PDF only.
public sealed record DocumentDto(
    Guid Id,
    DocumentType Type,
    DocumentStatus Status,
    string? Number,
    string? RelatedDocumentNumber,
    string From,
    string To,
    string Currency,
    decimal Subtotal,
    decimal Total,
    decimal AmountSettled,
    decimal BalanceRemaining,
    string PdfUrl,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    public static DocumentDto FromEntity(Document d) => new(
        d.Id, d.Type, d.Status, d.Number, d.RelatedDocumentNumber, d.From, d.To, d.Currency,
        d.Subtotal, d.Total, d.AmountSettled, d.BalanceRemaining,
        $"/documents/{d.Id}/pdf", d.CreatedAt, d.UpdatedAt);
}

public sealed record DocumentListDto(
    IReadOnlyList<DocumentDto> Data,
    int Page,
    int PerPage,
    int Total);
