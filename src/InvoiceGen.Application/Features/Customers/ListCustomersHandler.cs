using ErrorOr;
using InvoiceGen.Application.Common;
using Microsoft.EntityFrameworkCore;

namespace InvoiceGen.Application.Features.Customers;

public sealed class ListCustomersHandler(IAppDbContext db)
{
    // The user's active customers, ordered by name, paginated. Soft-deleted rows are
    // excluded by the global query filter.
    public async Task<ErrorOr<CustomerListDto>> HandleAsync(
        Guid userId, int page, int perPage, CancellationToken cancellationToken)
    {
        page = page < 1 ? 1 : page;
        if (perPage < 1) perPage = 20;          // invalid/zero → default
        else if (perPage > 30) perPage = 30;    // too high → clamp to max

        var query = db.Customers.AsNoTracking().Where(c => c.UserId == userId);

        var total = await query.CountAsync(cancellationToken);

        var customers = await query
            .OrderBy(c => c.Name)
            .ThenBy(c => c.Id) // stable ordering when names collide
            .Skip((page - 1) * perPage)
            .Take(perPage)
            .ToListAsync(cancellationToken);

        return new CustomerListDto(
            customers.Select(CustomerDto.FromEntity).ToList(),
            page, perPage, total);
    }
}
