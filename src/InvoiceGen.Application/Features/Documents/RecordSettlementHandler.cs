using ErrorOr;
using InvoiceGen.Application.Common;
using Microsoft.EntityFrameworkCore;

namespace InvoiceGen.Application.Features.Documents;

// A single delta. Positive records a payment/refund received; negative corrects a previous
// mistaken entry (there is no separate payment history — this is the only way to fix one).
public sealed record RecordSettlementRequest(decimal Amount);

public sealed class RecordSettlementHandler(IAppDbContext db, TimeProvider clock)
{
    public async Task<ErrorOr<DocumentDto>> HandleAsync(
        Guid userId, Guid documentId, RecordSettlementRequest request, CancellationToken cancellationToken)
    {
        var document = await db.Documents
            .Include(d => d.Items)
            .FirstOrDefaultAsync(d => d.Id == documentId && d.UserId == userId, cancellationToken);

        // 404 (not 403) for another user's document — hides its existence.
        if (document is null)
            return Error.NotFound("document_not_found", "Document not found.");

        // Apply the delta to the running total. Direction ("paid" vs "refunded") depends on
        // Type (x-settlement-policy) but the arithmetic and the [0, total] bound are identical,
        // so the messages stay type-neutral ("settled", not "paid").
        var newAmountSettled = document.AmountSettled + request.Amount;

        if (newAmountSettled < 0m)
            return Error.Validation("settlement_below_zero",
                $"The settled amount cannot go below 0. It is currently {document.AmountSettled}, " +
                $"so an amount of {request.Amount} would take it to {newAmountSettled}. " +
                "Use a smaller correction, or one that keeps the total settled at or above 0.",
                new Dictionary<string, object> { ["field"] = "amount" });

        if (newAmountSettled > document.Total)
            return Error.Validation("settlement_exceeds_total",
                $"The settled amount cannot exceed the document total of {document.Total}. " +
                $"It is currently {document.AmountSettled}, so an amount of {request.Amount} would " +
                $"take it to {newAmountSettled}. Record at most {document.Total - document.AmountSettled}.",
                new Dictionary<string, object> { ["field"] = "amount" });

        document.SetAmountSettled(newAmountSettled, clock.GetUtcNow());
        await db.SaveChangesAsync(cancellationToken);

        return DocumentDto.FromEntity(document);
    }
}
