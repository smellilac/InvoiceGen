using ErrorOr;
using InvoiceGen.Application.Common;
using Microsoft.EntityFrameworkCore;

namespace InvoiceGen.Application.Features.Documents;

public sealed class GetDocumentHandler(IAppDbContext db)
{
    public async Task<ErrorOr<DocumentDto>> HandleAsync(
        Guid userId, Guid documentId, CancellationToken cancellationToken)
    {
        var document = await db.Documents.AsNoTracking()
            .Include(d => d.Items)
            .FirstOrDefaultAsync(d => d.Id == documentId && d.UserId == userId, cancellationToken);

        // 404 (not 403) for another user's document — hides its existence.
        if (document is null)
            return Error.NotFound("document_not_found", "Document not found.");

        return DocumentDto.FromEntity(document);
    }
}
