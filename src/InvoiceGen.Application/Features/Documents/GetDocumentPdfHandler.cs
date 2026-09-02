using ErrorOr;
using InvoiceGen.Application.Common;
using Microsoft.EntityFrameworkCore;

namespace InvoiceGen.Application.Features.Documents;

public sealed class GetDocumentPdfHandler(IAppDbContext db, IPdfRenderer renderer)
{
    public async Task<ErrorOr<PdfFile>> HandleAsync(
        Guid userId, Guid documentId, CancellationToken cancellationToken)
    {
        var document = await db.Documents.AsNoTracking()
            .Include(d => d.Items)
            .FirstOrDefaultAsync(d => d.Id == documentId && d.UserId == userId, cancellationToken);

        if (document is null)
            return Error.NotFound("document_not_found", "Document not found.");

        var content = renderer.Render(document);
        var label = string.IsNullOrWhiteSpace(document.Number) ? document.Id.ToString() : document.Number;
        return new PdfFile(content, $"{DocumentTypeApi.ToApi(document.Type)}-{label}.pdf");
    }
}
