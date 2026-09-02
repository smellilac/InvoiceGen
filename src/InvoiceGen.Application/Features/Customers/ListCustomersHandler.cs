using ErrorOr;
using InvoiceGen.Application.Common;
using Microsoft.EntityFrameworkCore;

namespace InvoiceGen.Application.Features.Customers;

public sealed class ListCustomersHandler(IAppDbContext db)
{
    // Address book for the document-form picker: all of the user's active customers,
    // ordered by name. Soft-deleted rows are excluded by the global query filter.
    public async Task<ErrorOr<List<CustomerDto>>> HandleAsync(Guid userId, CancellationToken cancellationToken)
    {
        var customers = await db.Customers.AsNoTracking()
            .Where(c => c.UserId == userId)
            .OrderBy(c => c.Name)
            .ToListAsync(cancellationToken);

        return customers.Select(CustomerDto.FromEntity).ToList();
    }
}
