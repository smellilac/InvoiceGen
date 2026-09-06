using ErrorOr;
using InvoiceGen.Application.Common;
using Microsoft.EntityFrameworkCore;

namespace InvoiceGen.Application.Features.Documents;

public sealed class SendDocumentHandler(IAppDbContext db, IEmailQueue queue, TimeProvider clock)
{
    public async Task<ErrorOr<DocumentDto>> HandleAsync(
        Guid userId, Guid documentId, SendDocumentRequest request, CancellationToken cancellationToken)
    {
        var document = await db.Documents
            .Include(d => d.Items)
            .FirstOrDefaultAsync(d => d.Id == documentId && d.UserId == userId, cancellationToken);
        if (document is null)
            return Error.NotFound("document_not_found", "Document not found.");

        // Recipient resolution (x-email-delivery-policy): explicit to_email wins; otherwise the
        // linked customer's CURRENT email — a LIVE lookup (soft-deleted customers are filtered
        // out by the global query filter, so they resolve to null and fall through to 422).
        var recipient = request.ToEmail;
        if (string.IsNullOrWhiteSpace(recipient) && document.CustomerId is { } customerId)
        {
            var customer = await db.Customers
                .FirstOrDefaultAsync(c => c.Id == customerId && c.UserId == userId, cancellationToken);
            recipient = customer?.Email;
        }
        if (string.IsNullOrWhiteSpace(recipient))
            return Error.Validation("recipient_unresolved",
                "No email address could be resolved — provide to_email, or link a customer that has an email.");

        // Sync part: count the attempt + mark queued, persist, enqueue. The worker does the rest.
        document.MarkSendEnqueued(clock.GetUtcNow());
        await db.SaveChangesAsync(cancellationToken);

        await queue.EnqueueAsync(new EmailSendJob(document.Id, recipient, request.Message), cancellationToken);

        return DocumentDto.FromEntity(document);
    }
}
