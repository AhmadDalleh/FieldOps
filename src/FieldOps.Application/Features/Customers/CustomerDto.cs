using FieldOps.Domain.Customers;

namespace FieldOps.Application.Features.Customers;

public sealed record CustomerDto(
    Guid Id,
    string Code,
    string Name,
    CustomerType Type,
    string? Email,
    string Phone,
    string? TaxRegistrationNumber,
    string? BillingAddress,
    string? Notes,
    bool IsActive,
    DateTimeOffset CreatedAt)
{
    public static CustomerDto From(Customer c) =>
        new(c.Id, c.Code, c.Name, c.Type, c.Email, c.Phone, c.TaxRegistrationNumber, c.BillingAddress, c.Notes, c.IsActive, c.CreatedAt);
}

public sealed record CustomerListItem(Guid Id, string Code, string Name, CustomerType Type, string Phone, string? Email, bool IsActive);
