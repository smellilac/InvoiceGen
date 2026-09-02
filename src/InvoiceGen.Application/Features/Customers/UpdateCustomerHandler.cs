using ErrorOr;
using InvoiceGen.Application.Common;
using Microsoft.EntityFrameworkCore;

namespace InvoiceGen.Application.Features.Customers;

public sealed class UpdateCustomerHandler(IAppDbContext db, TimeProvider clock)
{
    public async Task<ErrorOr<CustomerDto>> HandleAsync(
        Guid userId, Guid customerId, UpdateCustomerRequest request, CancellationToken cancellationToken)
    {
        var customer = await db.Customers
            .FirstOrDefaultAsync(c => c.Id == customerId && c.UserId == userId, cancellationToken);

        if (customer is null)
            return Error.NotFound("customer_not_found", "Customer not found.");

        // PATCH semantics: only overwrite fields the caller actually supplied.
        if (request.Name is not null) customer.Name = request.Name;
        if (request.Email is not null) customer.Email = request.Email;
        if (request.Address is not null) customer.Address = request.Address;
        if (request.Phone is not null) customer.Phone = request.Phone;
        if (request.Notes is not null) customer.Notes = request.Notes;

        customer.UpdatedAt = clock.GetUtcNow();
        await db.SaveChangesAsync(cancellationToken);

        return CustomerDto.FromEntity(customer);
    }
}
