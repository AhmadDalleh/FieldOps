using FieldOps.Domain.Common;

namespace FieldOps.Domain.Customers;

public sealed class CustomerContact : AuditableEntity
{
    private CustomerContact() { }

    internal CustomerContact(Guid customerId, ContactDetails details)
    {
        CustomerId = customerId;
        Update(details);
    }

    public Guid CustomerId { get; private init; }
    public string Name { get; private set; } = null!;
    public string? Email { get; private set; }
    public string? Phone { get; private set; }
    public string? JobTitle { get; private set; }
    public bool IsPrimary { get; private set; }

    internal void Update(ContactDetails details)
    {
        Name = details.Name;
        Email = details.Email;
        Phone = details.Phone;
        JobTitle = details.JobTitle;
        IsPrimary = details.IsPrimary;
    }

    internal void ClearPrimary() => IsPrimary = false;
}

public sealed record ContactDetails(string Name, string? Email, string? Phone, string? JobTitle, bool IsPrimary);
