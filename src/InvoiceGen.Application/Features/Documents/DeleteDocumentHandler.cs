using ErrorOr;
using InvoiceGen.Application.Common;
using Microsoft.EntityFrameworkCore;

namespace InvoiceGen.Application.Features.Documents;

public sealed class DeleteDocumentHandler(IAppDbContext db, TimeProvider clock)
{
    // Soft delete: stamp DeletedAt so the row stays for history, but the global query filter
    // hides it from list/get/pdf/send. Same convention as DeleteCustomerHandler.
    public async Task<ErrorOr<Success>> HandleAsync(
        Guid userId, Guid documentId, CancellationToken cancellationToken)
    {
        var document = await db.Documents
            .FirstOrDefaultAsync(d => d.Id == documentId && d.UserId == userId, cancellationToken);

        if (document is null)
            return Error.NotFound("document_not_found", "Document not found.");

        document.DeletedAt = clock.GetUtcNow();
        await db.SaveChangesAsync(cancellationToken);

        return Result.Success;
    }
}
