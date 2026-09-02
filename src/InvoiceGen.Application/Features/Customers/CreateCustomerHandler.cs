using ErrorOr;
using InvoiceGen.Application.Common;
using InvoiceGen.Domain.Entities;

namespace InvoiceGen.Application.Features.Customers;

public sealed class CreateCustomerHandler(IAppDbContext db, TimeProvider clock)
{
    public async Task<ErrorOr<CustomerDto>> HandleAsync(
        Guid userId, CreateCustomerRequest request, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var customer = CreateCustomer(userId, request, now);

        db.Customers.Add(customer);
        await db.SaveChangesAsync(cancellationToken);

        return CustomerDto.FromEntity(customer);
    }

    private static Customer CreateCustomer(Guid userId, CreateCustomerRequest request, DateTimeOffset now)
    {
        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = request.Name,
            Email = request.Email,
            Address = request.Address,
            Phone = request.Phone,
            Notes = request.Notes,
            CreatedAt = now,
            UpdatedAt = now
        };
        return customer;
    }
}
