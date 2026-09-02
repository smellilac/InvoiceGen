using ErrorOr;
using InvoiceGen.Application.Common;
using Microsoft.EntityFrameworkCore;

namespace InvoiceGen.Application.Features.Customers;

public sealed class GetCustomerHandler(IAppDbContext db)
{
    public async Task<ErrorOr<CustomerDto>> HandleAsync(
        Guid userId, Guid customerId, CancellationToken cancellationToken)
    {
        var customer = await db.Customers.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == customerId && c.UserId == userId, cancellationToken);

        // 404 covers not-found, another user's, and soft-deleted (filtered out).
        if (customer is null)
            return Error.NotFound("customer_not_found", "Customer not found.");

        return CustomerDto.FromEntity(customer);
    }
}
