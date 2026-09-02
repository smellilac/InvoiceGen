using ErrorOr;
using InvoiceGen.Application.Common;
using Microsoft.EntityFrameworkCore;

namespace InvoiceGen.Application.Features.Customers;

public sealed class DeleteCustomerHandler(IAppDbContext db, TimeProvider clock)
{
    // Soft delete: stamp DeletedAt so the row stays for historical documents (frozen `to`
    // snapshot + customer_id reference), but is hidden and unusable for new documents.
    public async Task<ErrorOr<Success>> HandleAsync(
        Guid userId, Guid customerId, CancellationToken cancellationToken)
    {
        var customer = await db.Customers
            .FirstOrDefaultAsync(c => c.Id == customerId && c.UserId == userId, cancellationToken);

        if (customer is null)
            return Error.NotFound("customer_not_found", "Customer not found.");

        customer.DeletedAt = clock.GetUtcNow();
        await db.SaveChangesAsync(cancellationToken);

        return Result.Success;
    }
}
