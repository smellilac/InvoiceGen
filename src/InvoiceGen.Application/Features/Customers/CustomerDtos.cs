using InvoiceGen.Domain.Entities;

namespace InvoiceGen.Application.Features.Customers;

public sealed record CreateCustomerRequest(
    string Name,
    string? Email,
    string? Address,
    string? Phone,
    string? Notes);

public sealed record UpdateCustomerRequest(
    string? Name,
    string? Email,
    string? Address,
    string? Phone,
    string? Notes);

public sealed record CustomerDto(
    Guid Id,
    string Name,
    string? Email,
    string? Address,
    string? Phone,
    string? Notes,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    public static CustomerDto FromEntity(Customer c) => new(
        c.Id, c.Name, c.Email, c.Address, c.Phone, c.Notes, c.CreatedAt, c.UpdatedAt);
}
