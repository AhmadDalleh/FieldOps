using FieldOps.Application.Common;
using FieldOps.Application.Features.WorkOrders;
using FieldOps.Application.Tests.Infrastructure;
using FieldOps.Domain.Common;
using FieldOps.Domain.WorkOrders;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace FieldOps.Application.Tests.WorkOrders;

public class FieldWorkTests(PostgresFixture fixture) : TestBase(fixture)
{
    private static readonly LocationInput Dubai = new(25.2, 55.27);

    /// <summary>A dispatched job of a fresh technician, who is signed in afterwards.</summary>
    private async Task<(WorkOrderDto WorkOrder, (Guid UserId, Guid TechnicianId) Tech)> GivenDispatchedJob()
    {
        await SignInOffice();
        var (c, s) = await GivenCustomerAndSite();
        var tech = await GivenTechnician();
        var wo = await GivenWorkOrder(c, s);
        await Assign(wo.Id, tech.TechnicianId);
        await Advance(wo.Id, (w, u, now) => w.Dispatch(u, now));
        SignInTechnician(tech);
        return (wo, tech);
    }

    private Task<Result<WorkOrderDto>> EnRoute(Guid id, LocationInput? at = null) =>
        Resolve<EnRouteHandler>().Handle(new EnRouteCommand(id, at ?? new LocationInput(null, null)), default);

    private Task<Result<WorkOrderDto>> Start(Guid id) =>
        Resolve<StartWorkOrderHandler>().Handle(new StartWorkOrderCommand(id, new LocationInput(null, null)), default);

    private Task<Result<WorkOrderDto>> Complete(Guid id, Guid? signature, string? skipped = null) =>
        Resolve<CompleteWorkOrderHandler>().Handle(
            new CompleteWorkOrderCommand(id, new CompleteInput("Replaced the capacitor", "Sara M.", signature, skipped)), default);

    [Fact]
    public async Task On_my_way_moves_to_EnRoute_starts_travel_and_keeps_the_location()
    {
        var (wo, tech) = await GivenDispatchedJob();

        (await EnRoute(wo.Id, Dubai)).Value.Status.ShouldBe(WorkOrderStatus.EnRoute);

        var db = NewDb();
        var travel = await db.TimeEntries.SingleAsync(e => e.WorkOrderId == wo.Id);
        travel.Type.ShouldBe(TimeEntryType.Travel);
        travel.TechnicianId.ShouldBe(tech.TechnicianId);
        travel.EndedAt.ShouldBeNull();
        var history = await db.WorkOrderStatusHistory.Where(h => h.WorkOrderId == wo.Id).OrderBy(h => h.ChangedAt).ThenBy(h => h.Id).LastAsync();
        (history.Latitude, history.Longitude).ShouldBe((25.2, 55.27));
    }

    [Fact]
    public async Task Start_after_travel_stops_travel_and_starts_work()
    {
        var (wo, _) = await GivenDispatchedJob();
        await EnRoute(wo.Id);
        Fixture.Clock.Advance(TimeSpan.FromMinutes(25));

        var started = (await Start(wo.Id)).Value;

        started.Status.ShouldBe(WorkOrderStatus.InProgress);
        started.StartedAt.ShouldBe(Fixture.Clock.GetUtcNow());
        var entries = await NewDb().TimeEntries.Where(e => e.WorkOrderId == wo.Id).OrderBy(e => e.StartedAt).ToListAsync();
        entries.Select(e => (e.Type, e.DurationMinutes)).ShouldBe([(TimeEntryType.Travel, 25), (TimeEntryType.Work, null)]);
    }

    [Fact]
    public async Task Only_the_assigned_technician_can_do_field_work()
    {
        var (wo, _) = await GivenDispatchedJob();

        SignInTechnician(await GivenTechnician());
        (await EnRoute(wo.Id)).Error.ShouldBe(Errors.Forbidden);

        await SignInOffice();
        (await EnRoute(wo.Id)).Error.ShouldBe(Errors.Forbidden);
        (await Start(wo.Id)).Error.ShouldBe(Errors.Forbidden);
    }

    [Fact]
    public async Task Hold_stops_work_and_the_technician_resuming_starts_it_again()
    {
        var (wo, _) = await GivenDispatchedJob();
        await Start(wo.Id);
        Fixture.Clock.Advance(TimeSpan.FromMinutes(40));

        (await Resolve<HoldWorkOrderHandler>().Handle(new HoldWorkOrderCommand(wo.Id, new HoldInput("Waiting for part")), default))
            .Value.Status.ShouldBe(WorkOrderStatus.OnHold);
        (await NewDb().TimeEntries.SingleAsync(e => e.WorkOrderId == wo.Id)).DurationMinutes.ShouldBe(40);

        await Resolve<ResumeWorkOrderHandler>().Handle(new ResumeWorkOrderCommand(wo.Id), default);
        (await NewDb().TimeEntries.CountAsync(e => e.WorkOrderId == wo.Id && e.EndedAt == null)).ShouldBe(1);
    }

    [Fact]
    public async Task Complete_needs_a_signature_of_this_job_then_stops_work()
    {
        var (wo, tech) = await GivenDispatchedJob();
        await Start(wo.Id);

        (await Complete(wo.Id, Guid.NewGuid())).Error.ShouldBe(AttachmentErrors.InvalidSignature);
        var (otherWo, _) = await GivenDispatchedJob();
        await Start(otherWo.Id);
        var foreign = await GivenSignature(otherWo.Id);
        SignInTechnician(tech);
        (await Complete(wo.Id, foreign)).Error.ShouldBe(AttachmentErrors.InvalidSignature);

        Fixture.Clock.Advance(TimeSpan.FromMinutes(90));
        var signature = await GivenSignature(wo.Id);
        var done = (await Complete(wo.Id, signature)).Value;

        done.Status.ShouldBe(WorkOrderStatus.Completed);
        done.CompletedAt.ShouldBe(Fixture.Clock.GetUtcNow());
        done.SignedByName.ShouldBe("Sara M.");
        (await NewDb().TimeEntries.SingleAsync(e => e.WorkOrderId == wo.Id)).DurationMinutes.ShouldBe(90);
    }

    [Fact]
    public async Task Complete_with_open_tasks_needs_a_reason_which_goes_in_the_timeline()
    {
        var (wo, tech) = await GivenDispatchedJob();
        await SignInOffice();
        await Resolve<AddTaskHandler>().Handle(new AddTaskCommand(wo.Id, new TaskInput("Test run the unit")), default);
        SignInTechnician(tech);
        await Start(wo.Id);
        var signature = await GivenSignature(wo.Id);

        (await Complete(wo.Id, signature)).Error.ShouldBe(WorkOrderErrors.TasksNotDone);
        (await Complete(wo.Id, signature, "Customer asked us to stop")).Value.Status.ShouldBe(WorkOrderStatus.Completed);

        var history = (await Resolve<GetWorkOrderHistoryHandler>().Handle(new GetWorkOrderHistoryQuery(wo.Id), default)).Value;
        history[^1].Note.ShouldBe("Skipped tasks: Customer asked us to stop");
    }

    [Fact]
    public async Task Cancelling_stops_the_open_time_entry()
    {
        var (wo, _) = await GivenDispatchedJob();
        await EnRoute(wo.Id);
        Fixture.Clock.Advance(TimeSpan.FromMinutes(10));
        await SignInOffice();

        await Resolve<CancelWorkOrderHandler>().Handle(new CancelWorkOrderCommand(wo.Id, new CancelInput("Customer not home")), default);

        (await NewDb().TimeEntries.SingleAsync(e => e.WorkOrderId == wo.Id)).DurationMinutes.ShouldBe(10);
    }
}
