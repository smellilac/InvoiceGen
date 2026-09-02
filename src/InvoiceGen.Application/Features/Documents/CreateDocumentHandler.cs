using ErrorOr;
using InvoiceGen.Application.Common;
using InvoiceGen.Domain.Entities;
using InvoiceGen.Domain.Enums;
using Microsoft.AspNetCore.Identity;

namespace InvoiceGen.Application.Features.Documents;

public sealed class CreateDocumentHandler(
    IAppDbContext db,
    UserManager<AppUser> userManager,
    TimeProvider clock)
{
    public async Task<ErrorOr<DocumentDto>> HandleAsync(
        Guid userId, CreateDocumentRequest request, CancellationToken cancellationToken)
    {
        // Request-shape validation (items, to, per-item rules) runs in the endpoint's
        // ValidationFilter<CreateDocumentRequest> before this handler is called.

        // `from` may be omitted — fall back to the user's saved business profile.
        // This stays here (not in the validator) because it needs the user record.
        var from = request.From;
        if (string.IsNullOrWhiteSpace(from))
        {
            var user = await userManager.FindByIdAsync(userId.ToString());
            from = string.Join("\n", new[] { user?.BusinessName, user?.BusinessAddress }
                .Where(s => !string.IsNullOrWhiteSpace(s)));
        }
        if (string.IsNullOrWhiteSpace(from))
            return Error.Validation("from_required",
                "'from' is required, or set a business profile via PATCH /auth/me.");

        var document = BuildDocument(userId, request, from, clock.GetUtcNow());
        document.Recalculate();

        db.Documents.Add(document);
        await db.SaveChangesAsync(cancellationToken);

        return DocumentDto.FromEntity(document);
    }

    private static Document BuildDocument(
        Guid userId, CreateDocumentRequest request, string from, DateTimeOffset now) => new()
    {
        Id = Guid.NewGuid(),
        UserId = userId,
        Type = request.Type,
        Status = DocumentStatus.Generated, // PDF is rendered on demand, so always available
        Number = request.Number,
        RelatedDocumentNumber = request.RelatedDocumentNumber,
        From = from,
        To = request.To,
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
