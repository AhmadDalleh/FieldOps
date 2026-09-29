using FieldOps.Application.Features.Inventory;
using FieldOps.Application.Features.Notifications;
using FieldOps.Application.Features.Technicians;
using FieldOps.Application.Features.WorkOrders;
using FieldOps.Application.Tests.Infrastructure;
using FieldOps.Domain.Identity;
using FieldOps.Domain.Inventory;
using FieldOps.Domain.Notifications;
using FieldOps.Domain.WorkOrders;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace FieldOps.Application.Tests.Notifications;

public class NotificationTests(PostgresFixture fixture) : TestBase(fixture)
{
    private Guid _dispatcher;
    private Guid _admin;
    private (Guid UserId, Guid TechnicianId) _tech;
    private Guid _job;

    private RecordingNotifier Sent => Fixture.Notifier;

    /// <summary>A dispatcher (signed in), an admin, a technician and a new job.</summary>
    private async Task Given()
    {
        _dispatcher = (await SignInOffice()).Id;
        _admin = (await GivenUser(Role.Admin)).Id;
        _tech = await GivenTechnician();
        var (customer, site) = await GivenCustomerAndSite();
        _job = (await GivenWorkOrder(customer, site)).Id;
        Sent.Clear();
    }

    private Task<Domain.Common.Result<ScheduleResult>> Schedule(Guid technicianId, int startHour = 2) =>
        Resolve<ScheduleWorkOrderHandler>().Handle(new ScheduleWorkOrderCommand(_job, new ScheduleInput(
            technicianId, Fixture.Clock.GetUtcNow().AddHours(startHour), Fixture.Clock.GetUtcNow().AddHours(startHour + 2))), default);

    private async Task<List<Notification>> NotificationsOf(Guid userId) =>
        await NewDb().Notifications.Where(n => n.UserId == userId).OrderBy(n => n.CreatedAt).ToListAsync();

    [Fact]
    public async Task Scheduling_a_job_tells_the_technician_in_app_and_by_email()
    {
        await Given();

        (await Schedule(_tech.TechnicianId)).IsSuccess.ShouldBeTrue();

        var mine = (await NotificationsOf(_tech.UserId)).ShouldHaveSingleItem();
        mine.Type.ShouldBe(NotificationType.JobAssigned);
        mine.Title.ShouldStartWith("New job WO-");
        mine.Body!.ShouldContain("Acme, HQ, Dubai");
        mine.Link.ShouldBe($"/tech/jobs/{_job}");
        Sent.Pushed.ShouldHaveSingleItem().UserId.ShouldBe(_tech.UserId);
        var email = Sent.Emails.ShouldHaveSingleItem();
        email.Subject.ShouldBe(mine.Title);
        email.Body.ShouldContain($"http://localhost:4200/tech/jobs/{_job}");
        (await NotificationsOf(_dispatcher)).ShouldBeEmpty();

        var change = Sent.Changes.ShouldHaveSingleItem();
        change.ShouldSatisfyAllConditions(
            c => c.Id.ShouldBe(_job), c => c.Status.ShouldBe(WorkOrderStatus.Scheduled), c => c.TechnicianUserIds.ShouldBe([_tech.UserId]));
    }

    [Fact]
    public async Task Moving_a_job_tells_the_technician_and_handing_it_over_tells_both()
    {
        await Given();
        await Schedule(_tech.TechnicianId);

        await Schedule(_tech.TechnicianId, startHour: 4);
        (await NotificationsOf(_tech.UserId)).Last().Type.ShouldBe(NotificationType.JobRescheduled);

        var other = await GivenTechnician();
        await Schedule(other.TechnicianId, startHour: 4);
        (await NotificationsOf(other.UserId)).ShouldHaveSingleItem().Type.ShouldBe(NotificationType.JobAssigned);
        (await NotificationsOf(_tech.UserId)).Last().Type.ShouldBe(NotificationType.JobUnassigned);
        Sent.Emails.Count.ShouldBe(4);
        Sent.Changes.Last().TechnicianUserIds.ShouldBe([other.UserId, _tech.UserId], ignoreOrder: true);
    }

    [Fact]
    public async Task Unassigning_or_cancelling_tells_the_technician()
    {
        await Given();
        await Schedule(_tech.TechnicianId);

        await Resolve<UnassignWorkOrderHandler>().Handle(new UnassignWorkOrderCommand(_job), default);
        (await NotificationsOf(_tech.UserId)).Last().Type.ShouldBe(NotificationType.JobUnassigned);

        await Schedule(_tech.TechnicianId);
        (await Resolve<CancelWorkOrderHandler>().Handle(new CancelWorkOrderCommand(_job, new CancelInput("Customer called off")), default))
            .IsSuccess.ShouldBeTrue();
        var cancelled = (await NotificationsOf(_tech.UserId)).Last();
        cancelled.Type.ShouldBe(NotificationType.JobCancelled);
        cancelled.Body!.ShouldEndWith("Reason: Customer called off");
        Sent.Emails.Last().Subject.ShouldStartWith("Job cancelled");
    }

    [Fact]
    public async Task Hold_and_completion_go_to_the_office_but_not_to_whoever_did_it()
    {
        await Given();
        await Schedule(_tech.TechnicianId);
        await Advance(_job, (w, u, now) => w.Dispatch(u, now));
        SignInTechnician(_tech);
        await Resolve<StartWorkOrderHandler>().Handle(new StartWorkOrderCommand(_job, new LocationInput(null, null)), default);

        await Resolve<HoldWorkOrderHandler>().Handle(new HoldWorkOrderCommand(_job, new HoldInput("Waiting for a part")), default);
        await Resolve<ResumeWorkOrderHandler>().Handle(new ResumeWorkOrderCommand(_job), default);
        var signature = await GivenSignature(_job);
        (await Resolve<CompleteWorkOrderHandler>().Handle(new CompleteWorkOrderCommand(_job,
            new CompleteInput("Fixed", "Sara M.", signature, null)), default)).IsSuccess.ShouldBeTrue();

        foreach (var office in new[] { _dispatcher, _admin })
            (await NotificationsOf(office)).Select(n => (n.Type, n.Link)).ShouldBe([
                (NotificationType.JobOnHold, $"/office/work-orders/{_job}"),
                (NotificationType.JobCompleted, $"/office/work-orders/{_job}"),
            ]);
        (await NotificationsOf(_dispatcher))[0].Body.ShouldBe("Waiting for a part");
        (await NotificationsOf(_tech.UserId)).ShouldHaveSingleItem(); // only the original assignment
    }

    [Fact]
    public async Task Time_off_requests_go_to_the_office_and_decisions_back_to_the_technician()
    {
        await Given();
        SignInTechnician(_tech);
        var start = Fixture.Clock.GetUtcNow().AddDays(3);
        var request = (await Resolve<RequestTimeOffHandler>().Handle(new RequestTimeOffCommand(
            new TimeOffInput(start, start.AddDays(1), "Family")), default)).Value;

        (await NotificationsOf(_admin)).ShouldHaveSingleItem().ShouldSatisfyAllConditions(
            n => n.Type.ShouldBe(NotificationType.TimeOffRequested),
            n => n.Title.ShouldBe("Time off requested by Technician User"),
            n => n.Link.ShouldBe("/office/time-off"));

        Fixture.CurrentUser.SignInAs(_admin, Role.Admin);
        await Resolve<DecideTimeOffHandler>().Handle(new DecideTimeOffCommand(request.Id, Approve: true), default);
        (await NotificationsOf(_tech.UserId)).ShouldHaveSingleItem().Title.ShouldBe("Time off approved");
        (await NotificationsOf(_admin)).Count.ShouldBe(1);
        Sent.Emails.ShouldBeEmpty();
    }

    [Fact]
    public async Task Admins_hear_when_a_job_takes_a_part_below_its_reorder_level()
    {
        await Given();
        Fixture.CurrentUser.SignInAs(_admin, Role.Admin);
        var part = (await Resolve<CreatePartHandler>().Handle(new CreatePartCommand(
            new PartInput("FLT-01", "Filter", null, PartUnit.Pcs, 5, 10, ReorderLevel: 5)), default)).Value;
        var van = await NewDb().StockLocations.Where(l => l.TechnicianId == _tech.TechnicianId).Select(l => l.Id).SingleAsync();
        await Resolve<ReceiveStockHandler>().Handle(new ReceiveStockCommand(new ReceiveInput(part.Id, StockLocation.MainWarehouseId, 3)), default);
        await Resolve<TransferStockHandler>().Handle(new TransferStockCommand(new TransferInput(part.Id, StockLocation.MainWarehouseId, van, 3)), default);
        await Resolve<ReceiveStockHandler>().Handle(new ReceiveStockCommand(new ReceiveInput(part.Id, StockLocation.MainWarehouseId, 3)), default);
        Fixture.CurrentUser.SignInAs(_dispatcher, Role.Dispatcher);
        await Schedule(_tech.TechnicianId);
        await Advance(_job, (w, u, now) => w.Dispatch(u, now));
        await Advance(_job, (w, u, now) => w.Start(u, now));
        SignInTechnician(_tech);
        var use = (Guid job, decimal qty) => Resolve<AddWorkOrderPartHandler>().Handle(
            new AddWorkOrderPartCommand(job, new UsePartInput(part.Id, qty)), default);

        (await use(_job, 1)).IsSuccess.ShouldBeTrue(); // 6 → 5: not below 5 yet
        (await NotificationsOf(_admin)).ShouldBeEmpty();
        (await use(_job, 1)).IsSuccess.ShouldBeTrue(); // 5 → 4: crosses the level
        (await use(_job, 1)).IsSuccess.ShouldBeTrue(); // 4 → 3: already low, no repeat

        var low = (await NotificationsOf(_admin)).ShouldHaveSingleItem();
        low.Type.ShouldBe(NotificationType.LowStock);
        low.Body.ShouldBe("4 left in total; reorder level is 5.");
        (await NotificationsOf(_dispatcher)).Where(n => n.Type == NotificationType.LowStock).ShouldBeEmpty();
    }

    [Fact]
    public async Task A_mail_outage_does_not_stop_the_operation_or_the_in_app_notification()
    {
        await Given();
        Sent.FailEmails = true;

        (await Schedule(_tech.TechnicianId)).IsSuccess.ShouldBeTrue();

        (await NotificationsOf(_tech.UserId)).ShouldHaveSingleItem();
        Sent.Pushed.ShouldHaveSingleItem();
        (await NewDb().WorkOrders.SingleAsync(w => w.Id == _job)).Status.ShouldBe(WorkOrderStatus.Scheduled);
    }

    [Fact]
    public async Task A_refused_change_notifies_nobody()
    {
        await Given();

        (await Resolve<HoldWorkOrderHandler>().Handle(new HoldWorkOrderCommand(_job, new HoldInput("x")), default)).IsFailure.ShouldBeTrue();

        (await NewDb().Notifications.CountAsync()).ShouldBe(0);
        Sent.Pushed.ShouldBeEmpty();
        Sent.Changes.ShouldBeEmpty();
    }

    [Fact]
    public async Task Users_read_and_clear_only_their_own_notifications()
    {
        await Given();
        await Schedule(_tech.TechnicianId);
        await Schedule(_tech.TechnicianId, startHour: 5);
        var list = Resolve<ListNotificationsHandler>();

        (await list.Handle(new ListNotificationsQuery(), default)).Items.ShouldBeEmpty(); // the dispatcher's own list
        var theirs = (await NotificationsOf(_tech.UserId))[0];
        (await Resolve<MarkNotificationReadHandler>().Handle(new MarkNotificationReadCommand(theirs.Id), default))
            .Error.ShouldBe(NotificationErrors.NotFound);

        SignInTechnician(_tech);
        var mine = await list.Handle(new ListNotificationsQuery(), default);
        mine.UnreadCount.ShouldBe(2);
        mine.Items.Select(n => n.Type).ShouldBe([NotificationType.JobRescheduled, NotificationType.JobAssigned]);

        await Resolve<MarkNotificationReadHandler>().Handle(new MarkNotificationReadCommand(theirs.Id), default);
        (await list.Handle(new ListNotificationsQuery(UnreadOnly: true), default)).ShouldSatisfyAllConditions(
            l => l.UnreadCount.ShouldBe(1), l => l.Items.ShouldHaveSingleItem().Type.ShouldBe(NotificationType.JobRescheduled));

        await Resolve<MarkAllNotificationsReadHandler>().Handle(new MarkAllNotificationsReadCommand(), default);
        (await list.Handle(new ListNotificationsQuery(), default)).UnreadCount.ShouldBe(0);
    }
}
