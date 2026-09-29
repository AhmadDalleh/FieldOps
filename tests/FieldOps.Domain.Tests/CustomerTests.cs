using FieldOps.Domain.Customers;
using Shouldly;

namespace FieldOps.Domain.Tests;

public class CustomerTests
{
    private static Customer NewCustomer() =>
        Customer.Create("C-00001", new CustomerDetails("Acme", CustomerType.Business, null, "+971500000000", null, null, null));

    private static ContactDetails Contact(string name, bool primary) => new(name, null, null, null, primary);

    [Fact]
    public void Adding_a_primary_contact_unmarks_the_previous_primary()
    {
        var customer = NewCustomer();
        var first = customer.AddContact(Contact("First", true));

        var second = customer.AddContact(Contact("Second", true));

        first.IsPrimary.ShouldBeFalse();
        second.IsPrimary.ShouldBeTrue();
    }

    [Fact]
    public void Marking_an_existing_contact_primary_unmarks_the_previous_primary()
    {
        var customer = NewCustomer();
        var first = customer.AddContact(Contact("First", true));
        var second = customer.AddContact(Contact("Second", false));

        customer.UpdateContact(second.Id, Contact("Second", true));

        first.IsPrimary.ShouldBeFalse();
        second.IsPrimary.ShouldBeTrue();
    }

    [Fact]
    public void Adding_a_non_primary_contact_keeps_the_current_primary()
    {
        var customer = NewCustomer();
        var first = customer.AddContact(Contact("First", true));

        customer.AddContact(Contact("Second", false));

        first.IsPrimary.ShouldBeTrue();
    }

    [Fact]
    public void Updating_or_removing_an_unknown_contact_returns_not_found()
    {
        var customer = NewCustomer();

        customer.UpdateContact(Guid.CreateVersion7(), Contact("X", false)).Error.ShouldBe(CustomerErrors.ContactNotFound);
        customer.RemoveContact(Guid.CreateVersion7()).Error.ShouldBe(CustomerErrors.ContactNotFound);
    }
}
