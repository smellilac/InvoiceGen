using InvoiceGen.Domain.Entities;
using InvoiceGen.Domain.Enums;

namespace InvoiceGen.Application.Features.Documents;

// Maps a create request's fields onto a fresh Document entity (before Recalculate). Shared by the
// authenticated create (CreateDocumentHandler — which persists the result) and the guest create
// (CreateGuestDocumentHandler — which renders it and throws it away), so both build an identical
// document from identical inputs and run the identical money math (Document.Recalculate). The
// caller resolves `from`/`to`/`logoUrl` beforehand; the guest path passes Guid.Empty for userId
// and null for logoUrl (no owner, no saved customer, no profile logo).
internal static class DocumentBuilder
{
    public static Document Build(
        Guid userId, CreateDocumentRequest request, string from, string to, string? logoUrl, DateTimeOffset now) => new()
    {
        Id = Guid.NewGuid(),
        UserId = userId,
        CustomerId = request.CustomerId,
        Type = request.Type,
        Status = DocumentStatus.Generated, // PDF is rendered on demand, so always available
        Number = request.Number,
        RelatedDocumentNumber = request.RelatedDocumentNumber,
        From = from,
        To = to,
        LogoUrl = logoUrl,
        Currency = string.IsNullOrWhiteSpace(request.Currency) ? "USD" : request.Currency,
        Date = request.Date,
        DueDate = request.DueDate,
        TaxPercent = request.TaxPercent,
        DiscountPercent = request.DiscountPercent,
        ShippingAmount = request.ShippingAmount,
        AmountSettled = request.AmountSettled,
        Notes = request.Notes,
        Terms = request.Terms,
        CreatedAt = now,
        UpdatedAt = now,
        Items = request.Items.Select(i => new LineItem
        {
            Id = Guid.NewGuid(),
            Name = i.Name,
            Description = i.Description,
            Quantity = i.Quantity,
            UnitCost = i.UnitCost,
            Reference = i.Reference
        }).ToList()
    };
}
