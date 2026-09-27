using FieldOps.Application.Features.WorkOrders;
using FieldOps.Application.Tests.Infrastructure;
using FieldOps.Domain.Common;
using FieldOps.Domain.Identity;
using FieldOps.Domain.WorkOrders;
using Shouldly;

namespace FieldOps.Application.Tests.WorkOrders;

public class WorkOrderStatusTests(PostgresFixture fixture) : TestBase(fixture)
{
    private Task<Result<WorkOrderDto>> Hold(Guid id, string? note) =>
        Resolve<HoldWorkOrderHandler>().Handle(new HoldWorkOrderCommand(id, new HoldInput(note)), default);

    private Task<Result<WorkOrderDto>> Resume(Guid id) =>
        Resolve<ResumeWorkOrderHandler>().Handle(new ResumeWorkOrderCommand(id), default);

    private Task<Result<WorkOrderDto>> Cancel(Guid id, string? reason) =>
        Resolve<CancelWorkOrderHandler>().Handle(new CancelWorkOrderCommand(id, new CancelInput(reason)), default);

    private async Task<IReadOnlyList<WorkOrderHistoryItem>> History(Guid id) =>
        (await Resolve<GetWorkOrderHistoryHandler>().Handle(new GetWorkOrderHistoryQuery(id), default)).Value;

    private async Task<(WorkOrderDto WorkOrder, Guid TechUser, Guid Tech)> GivenInProgress()
    {
        var (c, s) = await GivenCustomerAndSite();
        var wo = await GivenWorkOrder(c, s);
        var (techUser, tech) = await GivenTechnician();
        await Assign(wo.Id, tech);
        await Advance(wo.Id, (w, u, now) => w.Dispatch(u, now));
        await Advance(wo.Id, (w, u, now) => w.Start(u, now));
        return (wo, techUser, tech);
    }

    [Fact]
    public async Task Office_holds_with_a_note_and_resumes()
    {
        var (wo, _, _) = await GivenInProgress();

        (await Hold(wo.Id, "Waiting for a compressor")).Value.Status.ShouldBe(WorkOrderStatus.OnHold);
        (await Resume(wo.Id)).Value.Status.ShouldBe(WorkOrderStatus.InProgress);

        var history = await History(wo.Id);
        history.Select(h => h.ToStatus).ShouldBe([WorkOrderStatus.New, WorkOrderStatus.Scheduled, WorkOrderStatus.Dispatched,
            WorkOrderStatus.InProgress, WorkOrderStatus.OnHold, WorkOrderStatus.InProgress]);
        history[4].Note.ShouldBe("Waiting for a compressor");
        history[4].ChangedByName.ShouldBe("Dispatcher User");
    }

    [Fact]
    public async Task Hold_without_a_note_is_rejected()
    {
        var (wo, _, _) = await GivenInProgress();

        (await Hold(wo.Id, " ")).Error.ShouldBe(WorkOrderErrors.NoteRequired);
    }

    [Fact]
    public async Task Assigned_technician_can_hold_and_resume_their_job()
    {
        var (wo, techUser, tech) = await GivenInProgress();
        Fixture.CurrentUser.SignInAs(techUser, Role.Technician, tech);

        (await Hold(wo.Id, "Need a ladder")).IsSuccess.ShouldBeTrue();
        (await Resume(wo.Id)).IsSuccess.ShouldBeTrue();
        (await History(wo.Id))[^1].ChangedByName.ShouldBe("Technician User");
    }

    [Fact]
    public async Task Other_technicians_cannot_touch_the_job()
    {
        var (wo, _, _) = await GivenInProgress();
        var (otherUser, other) = await GivenTechnician();
        Fixture.CurrentUser.SignInAs(otherUser, Role.Technician, other);

        (await Hold(wo.Id, "No")).Error.ShouldBe(Application.Common.Errors.Forbidden);
        (await Resolve<GetWorkOrderHandler>().Handle(new GetWorkOrderQuery(wo.Id), default)).Error.ShouldBe(Application.Common.Errors.Forbidden);
        (await Resolve<GetWorkOrderHistoryHandler>().Handle(new GetWorkOrderHistoryQuery(wo.Id), default)).Error
            .ShouldBe(Application.Common.Errors.Forbidden);
    }

    [Fact]
    public async Task Assigned_technician_can_read_their_job()
    {
        var (wo, techUser, tech) = await GivenInProgress();
        Fixture.CurrentUser.SignInAs(techUser, Role.Technician, tech);

        var read = (await Resolve<GetWorkOrderHandler>().Handle(new GetWorkOrderQuery(wo.Id), default)).Value;

        read.Technician!.Name.ShouldBe("Technician User");
        read.StartedAt.ShouldBe(PostgresFixture.Start);
    }

    [Fact]
    public async Task Office_cancels_with_a_reason()
    {
        await SignInOffice();
        var (c, s) = await GivenCustomerAndSite();
        var wo = await GivenWorkOrder(c, s);

        var cancelled = (await Cancel(wo.Id, "Customer fixed it")).Value;

        cancelled.Status.ShouldBe(WorkOrderStatus.Cancelled);
        cancelled.CancelReason.ShouldBe("Customer fixed it");
        cancelled.AllowedActions.ShouldBeEmpty();
        (await History(wo.Id))[^1].Note.ShouldBe("Customer fixed it");
    }

    [Fact]
    public async Task Cancel_needs_a_reason()
    {
        await SignInOffice();
        var (c, s) = await GivenCustomerAndSite();
        var wo = await GivenWorkOrder(c, s);

        (await Cancel(wo.Id, null)).Error.ShouldBe(WorkOrderErrors.ReasonRequired);
    }

    [Fact]
    public async Task Technician_cannot_cancel_even_their_own_job()
    {
        var (wo, techUser, tech) = await GivenInProgress();
        Fixture.CurrentUser.SignInAs(techUser, Role.Technician, tech);

        (await Cancel(wo.Id, "Too hard")).Error.ShouldBe(Application.Common.Errors.Forbidden);
    }

    [Fact]
    public async Task Completed_work_cannot_be_cancelled_or_held()
    {
        var (c, s) = await GivenCustomerAndSite();
        var wo = await GivenWorkOrder(c, s);
        var (_, tech) = await GivenTechnician();
        await Complete(wo.Id, tech);

        var cancel = await Cancel(wo.Id, "Late");
        cancel.Error.Code.ShouldBe("WorkOrder.InvalidTransition");
        cancel.Error.Type.ShouldBe(ErrorType.Conflict);
        (await Hold(wo.Id, "x")).Error.Code.ShouldBe("WorkOrder.InvalidTransition");
    }

    [Fact]
    public async Task Resume_from_a_status_other_than_on_hold_is_an_invalid_transition()
    {
        var (wo, _, _) = await GivenInProgress();

        (await Resume(wo.Id)).Error.Code.ShouldBe("WorkOrder.InvalidTransition");
    }
}
