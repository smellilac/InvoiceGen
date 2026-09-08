using ErrorOr;
using InvoiceGen.Application.Common;
using InvoiceGen.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace InvoiceGen.Application.Features.Documents;

public sealed class ListDocumentsHandler(IAppDbContext db)
{
    public async Task<ErrorOr<DocumentListDto>> HandleAsync(
        Guid userId, DocumentType? type, Guid? customerId, int page, int perPage, CancellationToken cancellationToken)
    {
        page = page < 1 ? 1 : page;
        if (perPage < 1) perPage = 10;          // invalid/zero → default
        else if (perPage > 30) perPage = 30;    // too high → clamp to max

        var query = db.Documents.AsNoTracking().Include(d => d.Items).Where(d => d.UserId == userId);
        if (type is not null)
            query = query.Where(d => d.Type == type);
        if (customerId is not null)
            query = query.Where(d => d.CustomerId == customerId);

        var total = await query.CountAsync(cancellationToken);

        var documents = await query
            .OrderByDescending(d => d.CreatedAt)
            .ThenByDescending(d => d.Id) // stable ordering when timestamps collide
            .Skip((page - 1) * perPage)
            .Take(perPage)
            .ToListAsync(cancellationToken);

        return new DocumentListDto(
            documents.Select(DocumentDto.FromEntity).ToList(),
            page, perPage, total);
    }
}
