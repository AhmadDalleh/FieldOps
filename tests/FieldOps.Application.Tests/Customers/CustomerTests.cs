using FieldOps.Application.Common;
using FieldOps.Application.Features.Customers;
using FieldOps.Application.Tests.Infrastructure;
using FieldOps.Domain.Customers;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace FieldOps.Application.Tests.Customers;

public class CustomerTests(PostgresFixture fixture) : TestBase(fixture)
{
    private static CustomerInput Input(string name = "Acme Trading", string phone = "+971501111111", string? email = "ops@acme.ae") =>
        new(name, CustomerType.Business, email, phone, null, "Business Bay, Dubai", null);

    private async Task<CustomerDto> GivenCustomer(string name = "Acme Trading", string? phone = null, string? email = null) =>
        (await Resolve<CreateCustomerHandler>().Handle(
            new CreateCustomerCommand(Input(name, phone ?? $"+9715{Random.Shared.Next(10000000, 99999999)}", email)), default)).Value;

    private Task<PagedResult<CustomerListItem>> Search(string? term, bool includeInactive = false) =>
        Resolve<ListCustomersHandler>().Handle(new ListCustomersQuery(new PageRequest(Search: term), includeInactive), default);

    [Fact]
    public async Task Creating_customers_assigns_sequential_codes()
    {
        var first = await GivenCustomer("First");
        var second = await GivenCustomer("Second");

        first.Code.ShouldBe("C-00001");
        second.Code.ShouldBe("C-00002");
        first.IsActive.ShouldBeTrue();
    }

    [Theory]
    [InlineData("", "+971500000000", null, false)]
    [InlineData("Acme", "", null, false)]
    [InlineData("Acme", "+971500000000", "not-an-email", false)]
    [InlineData("Acme", "+971500000000", null, true)]
    [InlineData("Acme", "+971500000000", "ops@acme.ae", true)]
    public void Name_and_phone_are_required_and_email_must_be_valid_if_given(string name, string phone, string? email, bool valid)
    {
        new CustomerInputValidator().Validate(Input(name, phone, email)).IsValid.ShouldBe(valid);
    }

    [Fact]
    public void Type_must_be_individual_or_business()
    {
        new CustomerInputValidator().Validate(Input() with { Type = (CustomerType)99 }).IsValid.ShouldBeFalse();
    }

    [Fact]
    public async Task Duplicate_phone_returns_a_conflict_warning_and_saves_nothing()
    {
        await GivenCustomer(phone: "+971509999999");

        var result = await Resolve<CreateCustomerHandler>().Handle(new CreateCustomerCommand(Input("Other", "+971509999999")), default);

        result.Error.ShouldBe(CustomerErrors.DuplicatePhone);
        (await NewDb().Customers.CountAsync()).ShouldBe(1);
    }

    [Fact]
    public async Task Duplicate_phone_can_be_saved_with_force()
    {
        await GivenCustomer(phone: "+971509999999");

        var result = await Resolve<CreateCustomerHandler>().Handle(new CreateCustomerCommand(Input("Other", "+971509999999"), Force: true), default);

        result.IsSuccess.ShouldBeTrue();
        (await NewDb().Customers.CountAsync()).ShouldBe(2);
    }

    [Theory]
    [InlineData("acme")]
    [InlineData("TRAD")]
    [InlineData("c-00001")]
    [InlineData("5012345")]
    [InlineData("OPS@ACME")]
    public async Task Search_is_case_insensitive_and_matches_part_of_name_code_phone_or_email(string term)
    {
        await GivenCustomer("Acme Trading", "+971501234567", "ops@acme.ae");
        await GivenCustomer("Zenith Cooling", "+971507654321", "hello@zenith.ae");

        var result = await Search(term);

        result.Items.Single().Name.ShouldBe("Acme Trading");
    }

    [Fact]
    public async Task Search_treats_wildcards_literally()
    {
        await GivenCustomer("100% Cool");
        await GivenCustomer("Other");

        (await Search("%")).Items.Single().Name.ShouldBe("100% Cool");
    }

    [Fact]
    public async Task Inactive_customers_are_hidden_unless_requested()
    {
        var inactive = await GivenCustomer("Gone");
        await GivenCustomer("Here");
        await Resolve<DeactivateCustomerHandler>().Handle(new DeactivateCustomerCommand(inactive.Id), default);

        (await Search(null)).Items.Select(c => c.Name).ShouldBe(["Here"]);
        (await Search(null, includeInactive: true)).Items.Select(c => c.Name).ShouldBe(["Gone", "Here"]);
    }

    [Fact]
    public async Task List_is_paged()
    {
        for (var i = 0; i < 3; i++) await GivenCustomer($"Customer {i}");

        var page = await Resolve<ListCustomersHandler>().Handle(new ListCustomersQuery(new PageRequest(2, 2)), default);

        page.TotalCount.ShouldBe(3);
        page.Items.Select(c => c.Name).ShouldBe(["Customer 2"]);
    }

    [Fact]
    public async Task Customer_can_be_viewed_and_edited()
    {
        var customer = await GivenCustomer();

        await Resolve<UpdateCustomerHandler>().Handle(
            new UpdateCustomerCommand(customer.Id, Input("Acme LLC") with { TaxRegistrationNumber = "100200300400003" }), default);

        var fetched = (await Resolve<GetCustomerHandler>().Handle(new GetCustomerQuery(customer.Id), default)).Value;
        fetched.Name.ShouldBe("Acme LLC");
        fetched.TaxRegistrationNumber.ShouldBe("100200300400003");
        fetched.Code.ShouldBe(customer.Code);
    }

    [Fact]
    public async Task Unknown_customer_returns_not_found()
    {
        var id = Guid.CreateVersion7();

        (await Resolve<GetCustomerHandler>().Handle(new GetCustomerQuery(id), default)).Error.ShouldBe(CustomerErrors.NotFound);
        (await Resolve<UpdateCustomerHandler>().Handle(new UpdateCustomerCommand(id, Input()), default)).Error.ShouldBe(CustomerErrors.NotFound);
        (await Resolve<DeactivateCustomerHandler>().Handle(new DeactivateCustomerCommand(id), default)).Error.ShouldBe(CustomerErrors.NotFound);
    }

    [Fact(Skip = "Enabled in Phase 4 once work orders exist (US-CUS-04 AC1).")]
    public Task Customer_with_open_work_orders_cannot_be_deactivated() => Task.CompletedTask;
}
