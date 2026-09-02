using ErrorOr;
using InvoiceGen.Application.Common;
using InvoiceGen.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace InvoiceGen.Application.Features.Documents;

public sealed class ListDocumentsHandler(IAppDbContext db)
{
    public async Task<ErrorOr<DocumentListDto>> HandleAsync(
        Guid userId, DocumentType? type, int page, int perPage, CancellationToken cancellationToken)
    {
        page = page < 1 ? 1 : page;
        if (perPage < 1) perPage = 20;          // invalid/zero → default
        else if (perPage > 30) perPage = 30;    // too high → clamp to max

        var query = db.Documents.AsNoTracking().Where(d => d.UserId == userId);
        if (type is not null)
            query = query.Where(d => d.Type == type);

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
