using FieldOps.Application.Features.Dispatch;
using FieldOps.Application.Features.Technicians;
using FieldOps.Application.Features.WorkOrders;
using FieldOps.Application.Tests.Infrastructure;
using FieldOps.Domain.Common;
using FieldOps.Domain.Technicians;
using FieldOps.Domain.WorkOrders;
using Shouldly;

namespace FieldOps.Application.Tests.WorkOrders;

public class SchedulingTests(PostgresFixture fixture) : TestBase(fixture)
{
    // 2 Oct 2026 in Dubai; 04:00 UTC is 08:00 local.
    private static readonly DateTimeOffset Morning = new(2026, 10, 2, 4, 0, 0, TimeSpan.Zero);

    private Task<Result<ScheduleResult>> Schedule(Guid id, Guid tech, DateTimeOffset start, TimeSpan length, bool allowOverlap = false) =>
        Resolve<ScheduleWorkOrderHandler>().Handle(new ScheduleWorkOrderCommand(id, new ScheduleInput(tech, start, start + length, allowOverlap)), default);

    private async Task<(Guid CustomerId, Guid SiteId, Guid Tech)> Given()
    {
        await SignInOffice();
        var (c, s) = await GivenCustomerAndSite();
        var (_, tech) = await GivenTechnician();
        return (c, s, tech);
    }

    [Fact]
    public async Task Scheduling_a_new_job_assigns_the_technician_and_moves_it_to_Scheduled()
    {
        var (c, s, tech) = await Given();
        var wo = await GivenWorkOrder(c, s);

        var result = (await Schedule(wo.Id, tech, Morning, TimeSpan.FromHours(2))).Value;

        result.WorkOrder.Status.ShouldBe(WorkOrderStatus.Scheduled);
        result.WorkOrder.Technician!.Id.ShouldBe(tech);
        result.WorkOrder.ScheduledStart.ShouldBe(Morning);
        result.WorkOrder.ScheduledEnd.ShouldBe(Morning.AddHours(2));
        result.Warnings.ShouldBeEmpty();
    }

    [Fact]
    public async Task Rescheduling_keeps_Scheduled_and_Dispatched_status()
    {
        var (c, s, tech) = await Given();
        var (_, other) = await GivenTechnician();
        var wo = await GivenWorkOrder(c, s);
        await Schedule(wo.Id, tech, Morning, TimeSpan.FromHours(2));

        (await Schedule(wo.Id, other, Morning.AddHours(3), TimeSpan.FromHours(1))).Value.WorkOrder.Status.ShouldBe(WorkOrderStatus.Scheduled);
        await Resolve<DispatchWorkOrderHandler>().Handle(new DispatchWorkOrderCommand(wo.Id), default);
        var moved = (await Schedule(wo.Id, tech, Morning.AddHours(5), TimeSpan.FromHours(1))).Value.WorkOrder;

        moved.Status.ShouldBe(WorkOrderStatus.Dispatched);
        moved.Technician!.Id.ShouldBe(tech);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-30)]
    [InlineData(14)]
    [InlineData(12 * 60 + 1)]
    public async Task Duration_must_be_between_15_minutes_and_12_hours(int minutes)
    {
        var (c, s, tech) = await Given();
        var wo = await GivenWorkOrder(c, s);

        (await Schedule(wo.Id, tech, Morning, TimeSpan.FromMinutes(minutes))).Error.ShouldBe(SchedulingErrors.InvalidDuration);
        new ScheduleInputValidator().Validate(new ScheduleInput(tech, Morning, Morning.AddMinutes(minutes))).IsValid.ShouldBeFalse();
    }

    [Theory]
    [InlineData(15)]
    [InlineData(12 * 60)]
    public async Task Duration_limits_are_inclusive(int minutes)
    {
        var (c, s, tech) = await Given();
        var wo = await GivenWorkOrder(c, s);

        (await Schedule(wo.Id, tech, Morning, TimeSpan.FromMinutes(minutes))).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task Approved_time_off_blocks_scheduling_but_pending_does_not()
    {
        var (c, s, tech) = await Given();
        var wo = await GivenWorkOrder(c, s);
        var db = NewDb();
        var approved = TimeOff.Request(tech, Morning.AddHours(1), Morning.AddHours(3), "Dentist").Value;
        approved.Approve();
        db.TimeOffs.AddRange(approved, TimeOff.Request(tech, Morning.AddHours(6), Morning.AddHours(8), "Maybe").Value);
        await db.SaveChangesAsync();

        (await Schedule(wo.Id, tech, Morning, TimeSpan.FromHours(2))).Error.ShouldBe(SchedulingErrors.OnTimeOff);
        (await Schedule(wo.Id, tech, Morning.AddHours(6), TimeSpan.FromHours(1))).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task Overlapping_another_job_needs_allow_overlap()
    {
        var (c, s, tech) = await Given();
        var first = await GivenWorkOrder(c, s);
        var second = await GivenWorkOrder(c, s);
        await Schedule(first.Id, tech, Morning, TimeSpan.FromHours(2));

        var blocked = await Schedule(second.Id, tech, Morning.AddHours(1), TimeSpan.FromHours(2));
        blocked.Error.Code.ShouldBe("Schedule.Overlap");
        var conflicts = (IReadOnlyList<ScheduledJob>)blocked.Error.Extensions!["conflicts"]!;
        conflicts.ShouldHaveSingleItem().Number.ShouldBe(first.Number);

        var forced = (await Schedule(second.Id, tech, Morning.AddHours(1), TimeSpan.FromHours(2), allowOverlap: true)).Value;
        forced.WorkOrder.Status.ShouldBe(WorkOrderStatus.Scheduled);
        forced.Warnings.ShouldBe([$"Overlaps {first.Number}"]);
    }

    [Fact]
    public async Task Back_to_back_jobs_do_not_overlap_and_closed_jobs_are_ignored()
    {
        var (c, s, tech) = await Given();
        var earlier = await GivenWorkOrder(c, s);
        var cancelled = await GivenWorkOrder(c, s);
        var next = await GivenWorkOrder(c, s);
        await Schedule(earlier.Id, tech, Morning, TimeSpan.FromHours(2));
        await Schedule(cancelled.Id, tech, Morning.AddHours(2), TimeSpan.FromHours(2), allowOverlap: true);
        await Resolve<CancelWorkOrderHandler>().Handle(new CancelWorkOrderCommand(cancelled.Id, new CancelInput("Dup")), default);

        (await Schedule(next.Id, tech, Morning.AddHours(2), TimeSpan.FromHours(2))).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task Missing_skill_is_a_warning_not_a_block()
    {
        var (c, s, tech) = await Given();
        var hvac = (await Resolve<CreateSkillHandler>().Handle(new CreateSkillCommand(new SkillInput("HVAC")), default)).Value;
        var wo = (await Resolve<CreateWorkOrderHandler>().Handle(new CreateWorkOrderCommand(new CreateWorkOrderInput(
            c, s, null, "AC", null, WorkOrderType.Repair, WorkOrderPriority.High, null, hvac.Id)), default)).Value;
        wo.RequiredSkill!.Name.ShouldBe("HVAC");

        var result = (await Schedule(wo.Id, tech, Morning, TimeSpan.FromHours(2))).Value;

        result.Warnings.ShouldBe(["Missing skill: HVAC"]);
        result.WorkOrder.Status.ShouldBe(WorkOrderStatus.Scheduled);
    }

    [Fact]
    public async Task Technician_with_the_skill_gets_no_warning()
    {
        var (c, s, tech) = await Given();
        var hvac = (await Resolve<CreateSkillHandler>().Handle(new CreateSkillCommand(new SkillInput("HVAC")), default)).Value;
        await Resolve<UpdateTechnicianHandler>().Handle(new UpdateTechnicianCommand(tech, new TechnicianInput(
            "TEC-900", null, "#1E88E5", 40, new TimeOnly(8, 0), new TimeOnly(17, 0), [hvac.Id])), default);
        var wo = (await Resolve<CreateWorkOrderHandler>().Handle(new CreateWorkOrderCommand(new CreateWorkOrderInput(
            c, s, null, "AC", null, WorkOrderType.Repair, WorkOrderPriority.High, null, hvac.Id)), default)).Value;

        (await Schedule(wo.Id, tech, Morning, TimeSpan.FromHours(2))).Value.Warnings.ShouldBeEmpty();
    }

    [Fact]
    public async Task Unknown_or_inactive_technician_is_rejected()
    {
        var (c, s, _) = await Given();
        var wo = await GivenWorkOrder(c, s);

        (await Schedule(wo.Id, Guid.NewGuid(), Morning, TimeSpan.FromHours(1))).Error.ShouldBe(SchedulingErrors.TechnicianNotAvailable);
    }

    [Fact]
    public async Task Closed_jobs_cannot_be_scheduled()
    {
        var (c, s, tech) = await Given();
        var wo = await GivenWorkOrder(c, s);
        await Resolve<CancelWorkOrderHandler>().Handle(new CancelWorkOrderCommand(wo.Id, new CancelInput("Dup")), default);

        (await Schedule(wo.Id, tech, Morning, TimeSpan.FromHours(1))).Error.Code.ShouldBe("WorkOrder.InvalidTransition");
    }

    [Fact]
    public async Task Unassign_returns_the_job_to_New_and_clears_technician_and_times()
    {
        var (c, s, tech) = await Given();
        var wo = await GivenWorkOrder(c, s);
        await Schedule(wo.Id, tech, Morning, TimeSpan.FromHours(2));
        await Resolve<DispatchWorkOrderHandler>().Handle(new DispatchWorkOrderCommand(wo.Id), default);

        var unassigned = (await Resolve<UnassignWorkOrderHandler>().Handle(new UnassignWorkOrderCommand(wo.Id), default)).Value;

        unassigned.Status.ShouldBe(WorkOrderStatus.New);
        unassigned.Technician.ShouldBeNull();
        unassigned.ScheduledStart.ShouldBeNull();
        unassigned.ScheduledEnd.ShouldBeNull();
    }

    [Fact]
    public async Task Unassigning_a_New_job_is_an_invalid_transition()
    {
        var (c, s, _) = await Given();
        var wo = await GivenWorkOrder(c, s);

        (await Resolve<UnassignWorkOrderHandler>().Handle(new UnassignWorkOrderCommand(wo.Id), default)).Error.Code
            .ShouldBe("WorkOrder.InvalidTransition");
    }

    [Fact]
    public async Task Dispatch_day_confirms_only_that_technicians_scheduled_jobs_starting_that_Dubai_day()
    {
        var (c, s, tech) = await Given();
        var (_, other) = await GivenTechnician();
        var today1 = await GivenWorkOrder(c, s, title: "today 1");
        var today2 = await GivenWorkOrder(c, s, title: "today 2");
        var tomorrow = await GivenWorkOrder(c, s, title: "tomorrow");
        var others = await GivenWorkOrder(c, s, title: "other tech");
        await Schedule(today2.Id, tech, Morning.AddHours(4), TimeSpan.FromHours(1));
        await Schedule(today1.Id, tech, Morning, TimeSpan.FromHours(1));
        // 2 Oct 21:00 UTC is already 3 Oct in Dubai.
        await Schedule(tomorrow.Id, tech, new DateTimeOffset(2026, 10, 2, 21, 0, 0, TimeSpan.Zero), TimeSpan.FromHours(1));
        await Schedule(others.Id, other, Morning, TimeSpan.FromHours(1));

        var result = (await Resolve<DispatchDayHandler>().Handle(
            new DispatchDayCommand(new DispatchDayInput(tech, new DateOnly(2026, 10, 2))), default)).Value;

        result.Dispatched.ShouldBe([today1.Number, today2.Number]);
        var get = Resolve<GetWorkOrderHandler>();
        (await get.Handle(new GetWorkOrderQuery(tomorrow.Id), default)).Value.Status.ShouldBe(WorkOrderStatus.Scheduled);
        (await get.Handle(new GetWorkOrderQuery(others.Id), default)).Value.Status.ShouldBe(WorkOrderStatus.Scheduled);
    }

    [Fact]
    public async Task Board_shows_the_days_jobs_time_off_and_unassigned_queue()
    {
        var (c, s, tech) = await Given();
        var scheduled = await GivenWorkOrder(c, s, title: "scheduled");
        await Schedule(scheduled.Id, tech, Morning, TimeSpan.FromHours(2));
        var nextDay = await GivenWorkOrder(c, s, title: "next day");
        await Schedule(nextDay.Id, tech, Morning.AddDays(1), TimeSpan.FromHours(2));
        await GivenWorkOrder(c, s, title: "low", priority: WorkOrderPriority.Low);
        await GivenWorkOrder(c, s, title: "urgent", priority: WorkOrderPriority.Urgent);
        await GivenWorkOrder(c, s, title: "high-late", priority: WorkOrderPriority.High, dueBy: Morning.AddDays(2));
        await GivenWorkOrder(c, s, title: "high-soon", priority: WorkOrderPriority.High, dueBy: Morning);
        var db = NewDb();
        var off = TimeOff.Request(tech, Morning.AddHours(6), Morning.AddHours(8), "Dentist").Value;
        off.Approve();
        db.TimeOffs.Add(off);
        await db.SaveChangesAsync();

        var board = await Resolve<GetDispatchBoardHandler>().Handle(new GetDispatchBoardQuery(new DateOnly(2026, 10, 2)), default);

        board.Technicians.ShouldHaveSingleItem().Id.ShouldBe(tech);
        var job = board.Jobs.ShouldHaveSingleItem();
        job.Title.ShouldBe("scheduled");
        job.Latitude.ShouldBe(25.2);
        job.SiteAddress.ShouldBe("Sheikh Zayed Road, Dubai");
        board.TimeOff.ShouldHaveSingleItem().Reason.ShouldBe("Dentist");
        board.Unassigned.Select(u => u.Title).ShouldBe(["urgent", "high-soon", "high-late", "low"]);
    }
}
