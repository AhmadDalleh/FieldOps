using FieldOps.Domain.WorkOrders;
using Shouldly;

namespace FieldOps.Domain.Tests;

public class TimeEntryTests
{
    private static readonly Guid User = Guid.NewGuid();
    private static readonly Guid Tech = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 6, 0, 0, TimeSpan.Zero);

    private static WorkOrder Dispatched(params string[] tasks)
    {
        var wo = WorkOrder.Create("WO-000001", Guid.NewGuid(), Guid.NewGuid(),
            new WorkOrderDetails("AC not cooling", null, WorkOrderType.Repair, WorkOrderPriority.Medium, null, null), tasks, User, Now);
        wo.Schedule(Tech, Now.AddHours(1), Now.AddHours(3), User, Now).IsSuccess.ShouldBeTrue();
        wo.Dispatch(User, Now).IsSuccess.ShouldBeTrue();
        return wo;
    }

    [Fact]
    public void On_my_way_starts_travel_and_records_the_location()
    {
        var wo = Dispatched();

        wo.EnRoute(User, Now, 25.2, 55.3).IsSuccess.ShouldBeTrue();

        var travel = wo.TimeEntries.ShouldHaveSingleItem();
        travel.Type.ShouldBe(TimeEntryType.Travel);
        travel.TechnicianId.ShouldBe(Tech);
        travel.IsOpen.ShouldBeTrue();
        wo.History[^1].Latitude.ShouldBe(25.2);
        wo.History[^1].Longitude.ShouldBe(55.3);
    }

    [Fact]
    public void Start_stops_travel_and_starts_work()
    {
        var wo = Dispatched();
        wo.EnRoute(User, Now);

        wo.Start(User, Now.AddMinutes(35)).IsSuccess.ShouldBeTrue();

        wo.StartedAt.ShouldBe(Now.AddMinutes(35));
        wo.TimeEntries[0].DurationMinutes.ShouldBe(35);
        wo.TimeEntries[1].Type.ShouldBe(TimeEntryType.Work);
        wo.TimeEntries[1].IsOpen.ShouldBeTrue();
    }

    [Fact]
    public void Start_straight_from_dispatched_starts_work_only()
    {
        var wo = Dispatched();

        wo.Start(User, Now).IsSuccess.ShouldBeTrue();

        wo.TimeEntries.ShouldHaveSingleItem().Type.ShouldBe(TimeEntryType.Work);
    }

    [Fact]
    public void Hold_stops_work_and_resume_by_the_technician_starts_it_again()
    {
        var wo = Dispatched();
        wo.Start(User, Now);

        wo.Hold("Waiting for part", User, Now.AddMinutes(50)).IsSuccess.ShouldBeTrue();
        wo.TimeEntries.ShouldHaveSingleItem().DurationMinutes.ShouldBe(50);

        wo.Resume(User, Now.AddHours(2), byTechnicianId: Tech).IsSuccess.ShouldBeTrue();
        wo.TimeEntries.Count.ShouldBe(2);
        wo.TimeEntries[1].IsOpen.ShouldBeTrue();
    }

    [Fact]
    public void Resume_by_the_office_does_not_start_work_time()
    {
        var wo = Dispatched();
        wo.Start(User, Now);
        wo.Hold("Waiting for part", User, Now.AddMinutes(50));

        wo.Resume(User, Now.AddHours(2)).IsSuccess.ShouldBeTrue();

        wo.TimeEntries.ShouldHaveSingleItem();
    }

    [Fact]
    public void Complete_and_cancel_stop_the_open_entry()
    {
        var completed = Dispatched();
        completed.Start(User, Now);
        completed.Complete("Done", "Sara", Guid.NewGuid(), User, Now.AddMinutes(90)).IsSuccess.ShouldBeTrue();
        completed.TimeEntries.ShouldHaveSingleItem().DurationMinutes.ShouldBe(90);

        var cancelled = Dispatched();
        cancelled.EnRoute(User, Now);
        cancelled.Cancel("Customer not home", User, Now.AddMinutes(20)).IsSuccess.ShouldBeTrue();
        cancelled.TimeEntries.ShouldHaveSingleItem().DurationMinutes.ShouldBe(20);
    }

    [Fact]
    public void Complete_needs_every_task_done_or_a_reason_for_skipping()
    {
        var wo = Dispatched("Check filter", "Test run");
        wo.Start(User, Now);
        wo.ToggleTask(wo.Tasks[0].Id, User, Now);

        wo.Complete("Done", "Sara", Guid.NewGuid(), User, Now).Error.ShouldBe(WorkOrderErrors.TasksNotDone);
        wo.Complete("Done", "Sara", Guid.NewGuid(), User, Now, "  ").Error.ShouldBe(WorkOrderErrors.TasksNotDone);

        wo.Complete("Done", "Sara", Guid.NewGuid(), User, Now, "Unit too hot to run").IsSuccess.ShouldBeTrue();
        wo.History[^1].Note.ShouldBe("Skipped tasks: Unit too hot to run");
    }

    [Fact]
    public void Correcting_an_entry_recomputes_its_duration()
    {
        var wo = Dispatched();
        wo.Start(User, Now);
        wo.Hold("Break", User, Now.AddHours(1));
        var entry = wo.TimeEntries[0];

        wo.CorrectTimeEntry(entry.Id, Now.AddMinutes(10), Now.AddMinutes(55), Now.AddHours(2)).IsSuccess.ShouldBeTrue();

        entry.StartedAt.ShouldBe(Now.AddMinutes(10));
        entry.DurationMinutes.ShouldBe(45);
    }

    [Theory]
    [InlineData(30, 30)]
    [InlineData(30, 10)]
    [InlineData(30, 200)]
    public void Correcting_rejects_an_end_before_the_start_or_in_the_future(int startMinutes, int endMinutes)
    {
        var wo = Dispatched();
        wo.Start(User, Now);
        wo.Hold("Break", User, Now.AddHours(1));

        wo.CorrectTimeEntry(wo.TimeEntries[0].Id, Now.AddMinutes(startMinutes), Now.AddMinutes(endMinutes), Now.AddMinutes(120))
            .Error.ShouldBe(WorkOrderErrors.InvalidTimeEntry);
    }

    [Fact]
    public void Only_a_running_entry_may_stay_without_an_end()
    {
        var wo = Dispatched();
        wo.Start(User, Now);

        wo.CorrectTimeEntry(wo.TimeEntries[0].Id, Now.AddMinutes(-5), null, Now.AddMinutes(30)).IsSuccess.ShouldBeTrue();
        wo.TimeEntries[0].IsOpen.ShouldBeTrue();

        wo.Hold("Break", User, Now.AddMinutes(40));
        wo.CorrectTimeEntry(wo.TimeEntries[0].Id, Now, null, Now.AddMinutes(60)).Error.ShouldBe(WorkOrderErrors.InvalidTimeEntry);
    }

    [Fact]
    public void Entries_are_locked_once_the_job_is_completed()
    {
        var wo = Dispatched();
        wo.Start(User, Now);
        wo.Complete("Done", "Sara", Guid.NewGuid(), User, Now.AddHours(1));

        wo.CorrectTimeEntry(wo.TimeEntries[0].Id, Now, Now.AddMinutes(30), Now.AddHours(2)).Error
            .ShouldBe(WorkOrderErrors.TimeEntriesLocked);
    }
}
