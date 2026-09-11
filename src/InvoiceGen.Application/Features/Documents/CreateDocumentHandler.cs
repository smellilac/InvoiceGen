using ErrorOr;
using InvoiceGen.Application.Common;
using InvoiceGen.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

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

        // include_logo defaults to true when omitted (see CreateDocumentRequest.IncludeLogo).
        var includeLogo = request.IncludeLogo ?? true;

        // The user record backs two creation-time snapshots: the `from` fallback and the logo.
        // Load it once if either needs it (both need the profile), not on every create.
        var from = request.From;
        AppUser? user = null;
        if (string.IsNullOrWhiteSpace(from) || includeLogo)
            user = await userManager.FindByIdAsync(userId.ToString());

        if (string.IsNullOrWhiteSpace(from))
        {
            from = string.Join("\n", new[] { user?.BusinessName, user?.BusinessAddress }
                .Where(s => !string.IsNullOrWhiteSpace(s)));
        }
        if (string.IsNullOrWhiteSpace(from))
            return Error.Validation("from_required",
                "'from' is required, or set a business profile via PATCH /auth/me.");

        // Optional saved customer — must belong to the caller and not be soft-deleted
        // (the global query filter excludes deleted ones, so a deleted id resolves to null).
        Customer? customer = null;
        if (request.CustomerId is { } customerId)
        {
            customer = await db.Customers
                .FirstOrDefaultAsync(c => c.Id == customerId && c.UserId == userId, cancellationToken);
            if (customer is null)
                return Error.Validation("customer_not_found",
                    "The referenced customer does not exist or has been deleted.");
        }

        // `to` is a frozen snapshot: if omitted and a customer is referenced, capture the
        // customer's CURRENT name/address now. Later customer edits never rewrite it.
        var to = request.To;
        if (string.IsNullOrWhiteSpace(to) && customer is not null)
            to = string.Join("\n", new[] { customer.Name, customer.Address }
                .Where(s => !string.IsNullOrWhiteSpace(s)));
        if (string.IsNullOrWhiteSpace(to))
            return Error.Validation("to_required", "'to' is required, or reference a customer.");

        // Freeze the profile logo URL onto the document now (a snapshot, like `to`/`from`) —
        // only when opted in and a logo is actually set. Never re-read from the profile after.
        var logoUrl = includeLogo && !string.IsNullOrWhiteSpace(user?.LogoUrl) ? user!.LogoUrl : null;

        var document = DocumentBuilder.Build(userId, request, from, to, logoUrl, clock.GetUtcNow());
        document.Recalculate();

        db.Documents.Add(document);
        await db.SaveChangesAsync(cancellationToken);

        return DocumentDto.FromEntity(document);
    }
}
