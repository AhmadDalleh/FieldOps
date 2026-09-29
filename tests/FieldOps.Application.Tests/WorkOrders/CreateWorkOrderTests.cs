using FieldOps.Application.Features.Assets;
using FieldOps.Application.Features.Customers;
using FieldOps.Application.Features.WorkOrders;
using FieldOps.Application.Tests.Infrastructure;
using FieldOps.Domain.Assets;
using FieldOps.Domain.WorkOrders;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace FieldOps.Application.Tests.WorkOrders;

public class CreateWorkOrderTests(PostgresFixture fixture) : TestBase(fixture)
{
    private Task<Domain.Common.Result<WorkOrderDto>> Create(CreateWorkOrderInput input) =>
        Resolve<CreateWorkOrderHandler>().Handle(new CreateWorkOrderCommand(input), default);

    private static CreateWorkOrderInput Input(Guid customerId, Guid siteId, Guid? assetId = null,
        WorkOrderType type = WorkOrderType.Repair, WorkOrderPriority priority = WorkOrderPriority.High) =>
        new(customerId, siteId, assetId, "AC not cooling", "Lobby unit is blowing warm air", type, priority, null);

    [Fact]
    public async Task Work_order_is_created_New_with_customer_site_and_a_history_row()
    {
        var office = await SignInOffice();
        var (customerId, siteId) = await GivenCustomerAndSite();

        var wo = (await Create(Input(customerId, siteId))).Value;

        wo.Number.ShouldBe("WO-000001");
        wo.Status.ShouldBe(WorkOrderStatus.New);
        wo.Customer.Name.ShouldBe("Acme");
        wo.Site.Name.ShouldBe("HQ");
        wo.Site.AccessNotes.ShouldBe("Gate 4411");
        wo.Priority.ShouldBe(WorkOrderPriority.High);
        wo.IsEditable.ShouldBeTrue();
        wo.AllowedActions.ShouldBe([WorkOrderAction.Schedule, WorkOrderAction.Cancel]);

        var history = (await Resolve<GetWorkOrderHistoryHandler>().Handle(new GetWorkOrderHistoryQuery(wo.Id), default)).Value;
        var row = history.ShouldHaveSingleItem();
        row.FromStatus.ShouldBeNull();
        row.ToStatus.ShouldBe(WorkOrderStatus.New);
        row.ChangedByName.ShouldBe(office.FullName);
        row.ChangedAt.ShouldBe(PostgresFixture.Start);
    }

    [Fact]
    public async Task Numbers_are_sequential()
    {
        await SignInOffice();
        var (customerId, siteId) = await GivenCustomerAndSite();

        var numbers = new List<string>();
        for (var i = 0; i < 3; i++) numbers.Add((await Create(Input(customerId, siteId))).Value.Number);

        numbers.ShouldBe(["WO-000001", "WO-000002", "WO-000003"]);
    }

    [Fact]
    public async Task A_rejected_work_order_does_not_use_up_a_number()
    {
        await SignInOffice();
        var (customerId, siteId) = await GivenCustomerAndSite();
        await Create(Input(customerId, Guid.NewGuid()));

        (await Create(Input(customerId, siteId))).Value.Number.ShouldBe("WO-000001");
    }

    [Fact]
    public async Task Site_must_belong_to_the_customer()
    {
        await SignInOffice();
        var (customerId, _) = await GivenCustomerAndSite("Acme");
        var (_, otherSite) = await GivenCustomerAndSite("Other");

        (await Create(Input(customerId, otherSite))).Error.ShouldBe(WorkOrderErrors.SiteNotOfCustomer);
    }

    [Fact]
    public async Task Customer_must_be_active()
    {
        await SignInOffice();
        var (customerId, siteId) = await GivenCustomerAndSite();
        await Resolve<DeactivateCustomerHandler>().Handle(new DeactivateCustomerCommand(customerId), default);

        (await Create(Input(customerId, siteId))).Error.ShouldBe(WorkOrderErrors.CustomerNotAvailable);
    }

    [Fact]
    public async Task Asset_is_optional_but_must_belong_to_the_site()
    {
        await SignInOffice();
        var (customerId, siteId) = await GivenCustomerAndSite();
        var (_, otherSite) = await GivenCustomerAndSite("Other");
        var foreignAsset = (await Resolve<RegisterAssetHandler>().Handle(new RegisterAssetCommand(otherSite,
            new AssetInput(AssetType.AC, "Split AC", null, null, null, null, null)), default)).Value;

        (await Create(Input(customerId, siteId, foreignAsset.Id))).Error.ShouldBe(WorkOrderErrors.AssetNotOfSite);
        (await Create(Input(customerId, siteId))).Value.Asset.ShouldBeNull();
    }

    [Fact]
    public async Task Asset_under_warranty_is_flagged_on_the_work_order()
    {
        await SignInOffice();
        var (customerId, siteId) = await GivenCustomerAndSite();
        var asset = (await Resolve<RegisterAssetHandler>().Handle(new RegisterAssetCommand(siteId,
            new AssetInput(AssetType.AC, "Split AC", "Daikin", null, "SN-1", null, new DateOnly(2027, 6, 1))), default)).Value;

        var wo = (await Create(Input(customerId, siteId, asset.Id))).Value;

        wo.Asset!.UnderWarranty.ShouldBeTrue();
        wo.Asset.WarrantyExpiresOn.ShouldBe(new DateOnly(2027, 6, 1));
    }

    [Theory]
    [InlineData("", false)]
    [InlineData("AC not cooling", true)]
    public void Title_is_required(string title, bool valid)
    {
        var input = Input(Guid.NewGuid(), Guid.NewGuid()) with { Title = title };
        new CreateWorkOrderInputValidator().Validate(input).IsValid.ShouldBe(valid);
    }

    [Fact]
    public void Customer_site_type_and_priority_are_required()
    {
        new CreateWorkOrderInputValidator().Validate(Input(Guid.Empty, Guid.NewGuid())).IsValid.ShouldBeFalse();
        new CreateWorkOrderInputValidator().Validate(Input(Guid.NewGuid(), Guid.Empty)).IsValid.ShouldBeFalse();
        new CreateWorkOrderInputValidator().Validate(Input(Guid.NewGuid(), Guid.NewGuid(), type: (WorkOrderType)9)).IsValid.ShouldBeFalse();
        new CreateWorkOrderInputValidator().Validate(Input(Guid.NewGuid(), Guid.NewGuid(), priority: (WorkOrderPriority)9)).IsValid.ShouldBeFalse();
    }

    [Fact]
    public async Task Active_checklist_template_for_the_type_is_copied_into_tasks()
    {
        await SignInOffice();
        var (customerId, siteId) = await GivenCustomerAndSite();
        var db = NewDb();
        var inactive = ChecklistTemplate.Create("Old maintenance", WorkOrderType.Maintenance, ["Old step"]);
        inactive.Deactivate();
        db.ChecklistTemplates.AddRange(
            inactive,
            ChecklistTemplate.Create("Maintenance", WorkOrderType.Maintenance, ["Clean filters", "Check pressure"]),
            ChecklistTemplate.Create("Inspection", WorkOrderType.Inspection, ["Walk the site"]));
        await db.SaveChangesAsync();

        var maintenance = (await Create(Input(customerId, siteId, type: WorkOrderType.Maintenance))).Value;
        var repair = (await Create(Input(customerId, siteId, type: WorkOrderType.Repair))).Value;

        maintenance.Tasks.Select(t => (t.SortOrder, t.Description, t.IsDone)).ShouldBe([(1, "Clean filters", false), (2, "Check pressure", false)]);
        repair.Tasks.ShouldBeEmpty();
        (await NewDb().WorkOrderTasks.CountAsync()).ShouldBe(2);
    }

    [Fact]
    public async Task Urgent_work_without_a_due_time_is_due_in_four_hours()
    {
        await SignInOffice();
        var (customerId, siteId) = await GivenCustomerAndSite();

        var wo = (await Create(Input(customerId, siteId, priority: WorkOrderPriority.Urgent))).Value;

        wo.DueBy.ShouldBe(PostgresFixture.Start.AddHours(4));
    }
}
