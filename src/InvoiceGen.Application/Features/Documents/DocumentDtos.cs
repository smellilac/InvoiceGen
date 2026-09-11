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
    decimal AmountSettled,
    // Opt out of stamping the user's profile logo onto this document. Null/omitted = true
    // (include it). When true and the profile has a logo, its URL is frozen onto the document
    // at creation (see Document.LogoUrl); after that it never tracks the profile.
    bool? IncludeLogo = true);

// The body for the unauthenticated POST /documents/guest flow. Same shape as
// CreateDocumentRequest minus CustomerId (a guest has no saved customers) and with From
// required (no saved profile to fall back on). No IncludeLogo — a guest has no profile logo.
// See GuestCreateDocumentRequest in openapi.yaml and x-guest-document-policy.
public sealed record GuestCreateDocumentRequest(
    DocumentType Type,
    string From,
    string To,
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

// Matches the OpenAPI `Document` schema. Echoes back every input the caller
// submitted on CreateDocumentRequest (line items, dates, the tax/discount/shipping
// inputs, notes/terms) alongside the computed totals, so a GET round-trips what a
// POST accepted and the frontend can prefill a form from it. The input-shaped fields
// reuse CreateDocumentRequest's exact names/shapes (incl. CreateLineItemRequest) so
// the round-trip is verbatim.
public sealed record DocumentDto(
    Guid Id,
    DocumentType Type,
    DocumentStatus Status,
    string? Number,
    string? RelatedDocumentNumber,
    Guid? CustomerId,
    string From,
    string To,
    // Frozen at creation from the user's profile logo (null if opted out or none set). Never a
    // live view of the current profile — see Document.LogoUrl.
    string? LogoUrl,
    string Currency,
    DateOnly Date,
    DateOnly? DueDate,
    IReadOnlyList<CreateLineItemRequest> Items,
    decimal Subtotal,
    decimal DiscountPercent,
    decimal DiscountAmount,
    decimal TaxPercent,
    decimal TaxAmount,
    decimal ShippingAmount,
    decimal Total,
    decimal AmountSettled,
    decimal BalanceRemaining,
    string? Notes,
    string? Terms,
    DateTimeOffset? LastSentAt,
    int SendCount,
    SendStatus? LastSendStatus,
    string? LastSendError,
    string PdfUrl,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    public static DocumentDto FromEntity(Document d) => new(
        d.Id, d.Type, d.Status, d.Number, d.RelatedDocumentNumber, d.CustomerId, d.From, d.To, d.LogoUrl, d.Currency,
        d.Date, d.DueDate,
        d.Items.Select(i => new CreateLineItemRequest(i.Name, i.Description, i.Quantity, i.UnitCost, i.Reference)).ToList(),
        d.Subtotal, d.DiscountPercent, d.DiscountAmount, d.TaxPercent, d.TaxAmount, d.ShippingAmount, d.Total,
        d.AmountSettled, d.BalanceRemaining,
        d.Notes, d.Terms,
        d.LastSentAt, d.SendCount, d.LastSendStatus, d.LastSendError,
        $"/documents/{d.Id}/pdf", d.CreatedAt, d.UpdatedAt);
}

public sealed record DocumentListDto(
    IReadOnlyList<DocumentDto> Data,
    int Page,
    int PerPage,
    int Total);
