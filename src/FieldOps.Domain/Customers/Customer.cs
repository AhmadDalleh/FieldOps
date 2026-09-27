using FieldOps.Domain.Common;

namespace FieldOps.Domain.Customers;

public enum CustomerType { Individual, Business }

public sealed class Customer : AuditableEntity
{
    private readonly List<CustomerContact> _contacts = [];

    private Customer() { }

    public string Code { get; private init; } = null!;
    public string Name { get; private set; } = null!;
    public CustomerType Type { get; private set; }
    public string? Email { get; private set; }
    public string Phone { get; private set; } = null!;
    public string? TaxRegistrationNumber { get; private set; }
    public string? BillingAddress { get; private set; }
    public string? Notes { get; private set; }
    public bool IsActive { get; private set; } = true;

    public IReadOnlyList<CustomerContact> Contacts => _contacts;

    public static Customer Create(string code, CustomerDetails details)
    {
        var customer = new Customer { Code = code };
        customer.Update(details);
        return customer;
    }

    public void Update(CustomerDetails details)
    {
        Name = details.Name;
        Type = details.Type;
        Email = details.Email;
        Phone = details.Phone;
        TaxRegistrationNumber = details.TaxRegistrationNumber;
        BillingAddress = details.BillingAddress;
        Notes = details.Notes;
    }

    /// <remarks>The open-work-order rule (US-CUS-04 AC1) is checked by the handler once work orders exist.</remarks>
    public void Deactivate() => IsActive = false;

    public CustomerContact AddContact(ContactDetails details)
    {
        var contact = new CustomerContact(Id, details);
        if (details.IsPrimary) ClearPrimaryExcept(contact);
        _contacts.Add(contact);
        return contact;
    }

    public Result UpdateContact(Guid contactId, ContactDetails details)
    {
        var contact = _contacts.FirstOrDefault(c => c.Id == contactId);
        if (contact is null) return CustomerErrors.ContactNotFound;

        contact.Update(details);
        if (details.IsPrimary) ClearPrimaryExcept(contact);
        return Result.Success();
    }

    public Result RemoveContact(Guid contactId)
    {
        var contact = _contacts.FirstOrDefault(c => c.Id == contactId);
        if (contact is null) return CustomerErrors.ContactNotFound;

        _contacts.Remove(contact);
        return Result.Success();
    }

    private void ClearPrimaryExcept(CustomerContact primary)
    {
        foreach (var other in _contacts.Where(c => c != primary)) other.ClearPrimary();
    }
}

public sealed record CustomerDetails(
    string Name,
    CustomerType Type,
    string? Email,
    string Phone,
    string? TaxRegistrationNumber,
    string? BillingAddress,
    string? Notes);
