using FieldOps.Application.Features.Customers;
using FieldOps.Application.Tests.Infrastructure;
using FieldOps.Domain.Customers;
using Shouldly;

namespace FieldOps.Application.Tests.Customers;

public class ContactTests(PostgresFixture fixture) : TestBase(fixture)
{
    private async Task<Guid> GivenCustomer() =>
        (await Resolve<CreateCustomerHandler>().Handle(new CreateCustomerCommand(
            new CustomerInput("Acme", CustomerType.Business, null, "+971500000000", null, null, null), Force: true), default)).Value.Id;

    private async Task<ContactDto> Add(Guid customerId, string name, bool primary) =>
        (await Resolve<AddContactHandler>().Handle(
            new AddContactCommand(customerId, new ContactInput(name, $"{name}@acme.ae", null, "Manager", primary)), default)).Value;

    private async Task<IReadOnlyList<ContactDto>> List(Guid customerId) =>
        (await Resolve<ListContactsHandler>().Handle(new ListContactsQuery(customerId), default)).Value;

    [Fact]
    public async Task Contacts_can_be_added_edited_and_removed()
    {
        var customerId = await GivenCustomer();
        var contact = await Add(customerId, "sara", primary: false);

        await Resolve<UpdateContactHandler>().Handle(
            new UpdateContactCommand(customerId, contact.Id, new ContactInput("Sara K", null, "+971500000001", null, false)), default);
        var afterEdit = await List(customerId);
        await Resolve<DeleteContactHandler>().Handle(new DeleteContactCommand(customerId, contact.Id), default);

        afterEdit.Single().Name.ShouldBe("Sara K");
        afterEdit.Single().Phone.ShouldBe("+971500000001");
        (await List(customerId)).ShouldBeEmpty();
    }

    [Fact]
    public async Task Marking_a_contact_primary_unmarks_the_previous_primary()
    {
        var customerId = await GivenCustomer();
        await Add(customerId, "first", primary: true);
        var second = await Add(customerId, "second", primary: false);

        await Resolve<UpdateContactHandler>().Handle(
            new UpdateContactCommand(customerId, second.Id, new ContactInput("second", null, null, null, true)), default);

        var contacts = await List(customerId);
        contacts.Single(c => c.IsPrimary).Name.ShouldBe("second");
    }

    [Fact]
    public async Task Adding_a_new_primary_contact_unmarks_the_previous_primary()
    {
        var customerId = await GivenCustomer();
        await Add(customerId, "first", primary: true);

        await Add(customerId, "second", primary: true);

        (await List(customerId)).Single(c => c.IsPrimary).Name.ShouldBe("second");
    }

    [Fact]
    public async Task Contact_of_another_customer_is_not_found()
    {
        var customerA = await GivenCustomer();
        var customerB = await GivenCustomer();
        var contact = await Add(customerA, "sara", primary: false);

        var result = await Resolve<DeleteContactHandler>().Handle(new DeleteContactCommand(customerB, contact.Id), default);

        result.Error.ShouldBe(CustomerErrors.ContactNotFound);
        (await List(customerA)).Count.ShouldBe(1);
    }
}
