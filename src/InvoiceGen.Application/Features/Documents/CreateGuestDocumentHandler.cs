namespace InvoiceGen.Application.Features.Documents;

// The unauthenticated "try before you sign up" create path (POST /documents/guest). It builds a
// transient Document from the request, runs the SAME money math and PDF renderer as the
// authenticated create, and returns the rendered bytes — WITHOUT persisting anything (no Document
// row, no line items). See x-guest-document-policy. Because there is no owner, saved customer, or
// profile logo, this handler needs no database access at all.
public sealed class CreateGuestDocumentHandler(IPdfRenderer renderer, TimeProvider clock)
{
    public PdfFile Handle(GuestCreateDocumentRequest request)
    {
        // Request-shape validation (from/to/items, per-item rules) runs in the endpoint's
        // ValidationFilter<GuestCreateDocumentRequest> before this handler is called.

        // Reuse the authenticated create's field mapping by projecting onto a CreateDocumentRequest:
        // no customer (CustomerId null) and no logo (IncludeLogo false) — a guest has neither.
        var mapped = new CreateDocumentRequest(
            request.Type, request.From, CustomerId: null, request.To, request.Date, request.DueDate,
            request.Number, request.RelatedDocumentNumber, request.Currency, request.Items,
            request.TaxPercent, request.DiscountPercent, request.ShippingAmount, request.Notes,
            request.Terms, request.AmountSettled, IncludeLogo: false);

        // Guid.Empty owner: the entity is transient — built, totaled, rendered, then discarded.
        // It is NEVER added to the DbContext, so nothing reaches the database.
        var document = DocumentBuilder.Build(Guid.Empty, mapped, request.From, request.To, logoUrl: null, clock.GetUtcNow());
        document.Recalculate();

        var content = renderer.Render(document, logo: null);
        var label = string.IsNullOrWhiteSpace(document.Number) ? "document" : document.Number;
        return new PdfFile(content, $"{DocumentTypeApi.ToApi(document.Type)}-{label}.pdf");
    }
}
