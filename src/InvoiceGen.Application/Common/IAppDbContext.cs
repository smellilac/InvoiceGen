using InvoiceGen.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace InvoiceGen.Application.Common;

// The Clean Architecture "application DbContext" seam: Application depends on this
// interface, Infrastructure's AppDbContext implements it. Keeps handlers testable
// and free of the concrete DbContext / provider.
public interface IAppDbContext
{
    DbSet<Document> Documents { get; }
    DbSet<LineItem> LineItems { get; }
    DbSet<Customer> Customers { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
