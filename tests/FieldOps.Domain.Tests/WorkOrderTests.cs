using FieldOps.Domain.WorkOrders;
using Shouldly;

namespace FieldOps.Domain.Tests;

public class WorkOrderTests
{
    private static readonly Guid User = Guid.NewGuid();
    private static readonly Guid Tech = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 6, 0, 0, TimeSpan.Zero);

    private static WorkOrderDetails Details(WorkOrderPriority priority = WorkOrderPriority.Medium, DateTimeOffset? dueBy = null) =>
        new("AC not cooling", "Lobby unit", WorkOrderType.Repair, priority, dueBy, null);

    private static WorkOrder NewWorkOrder(params string[] tasks) =>
        WorkOrder.Create("WO-000001", Guid.NewGuid(), Guid.NewGuid(), Details(), tasks, User, Now);

    /// <summary>Walks a new work order along valid transitions until it reaches <paramref name="status"/>.</summary>
    private static WorkOrder In(WorkOrderStatus status)
    {
        var wo = NewWorkOrder("Check filter");
        void Must(FieldOps.Domain.Common.Result r) => r.IsSuccess.ShouldBeTrue();

        if (status == WorkOrderStatus.Cancelled) { Must(wo.Cancel("Customer called off", User, Now)); return wo; }
        if (status == WorkOrderStatus.New) return wo;
        Must(wo.Schedule(Tech, Now.AddHours(1), Now.AddHours(3), User, Now));
        if (status == WorkOrderStatus.Scheduled) return wo;
        Must(wo.Dispatch(User, Now));
        if (status == WorkOrderStatus.Dispatched) return wo;
        if (status == WorkOrderStatus.EnRoute) { Must(wo.EnRoute(User, Now)); return wo; }
        Must(wo.Start(User, Now));
        if (status == WorkOrderStatus.InProgress) return wo;
        if (status == WorkOrderStatus.OnHold) { Must(wo.Hold("Waiting for parts", User, Now)); return wo; }
        wo.ToggleTask(wo.Tasks[0].Id, User, Now).IsSuccess.ShouldBeTrue();
        Must(wo.Complete("Replaced capacitor", "Sara M.", Guid.NewGuid(), User, Now));
        if (status == WorkOrderStatus.Completed) return wo;
        Must(wo.MarkInvoiced(User, Now));
        return wo;
    }

    private static FieldOps.Domain.Common.Result Act(WorkOrder wo, WorkOrderAction action) => action switch
    {
        WorkOrderAction.Schedule => wo.Schedule(Tech, Now.AddHours(4), Now.AddHours(6), User, Now),
        WorkOrderAction.Unassign => wo.Unassign(User, Now),
        WorkOrderAction.Dispatch => wo.Dispatch(User, Now),
        WorkOrderAction.EnRoute => wo.EnRoute(User, Now, 25.2, 55.3),
        WorkOrderAction.Start => wo.Start(User, Now),
        WorkOrderAction.Hold => wo.Hold("Waiting for parts", User, Now),
        WorkOrderAction.Resume => wo.Resume(User, Now),
        WorkOrderAction.Complete => wo.Complete("Done", "Sara M.", Guid.NewGuid(), User, Now, "Customer declined"),
        WorkOrderAction.Cancel => wo.Cancel("No longer needed", User, Now),
        WorkOrderAction.Invoice => wo.MarkInvoiced(User, Now),
        WorkOrderAction.VoidInvoice => wo.VoidInvoice(User, Now),
        _ => throw new ArgumentOutOfRangeException(nameof(action)),
    };

    // The transition table from docs/07-flows.md. Columns follow the WorkOrderAction order:
    // schedule, unassign, dispatch, enRoute, start, hold, resume, complete, cancel, invoice, voidInvoice. "-" means not allowed.
    private static readonly Dictionary<WorkOrderStatus, string> Table = new()
    {
        [WorkOrderStatus.New] = "Scheduled - - - - - - - Cancelled - -",
        [WorkOrderStatus.Scheduled] = "Scheduled New Dispatched - - - - - Cancelled - -",
        [WorkOrderStatus.Dispatched] = "Dispatched New - EnRoute InProgress - - - Cancelled - -",
        [WorkOrderStatus.EnRoute] = "- - - - InProgress - - - Cancelled - -",
        [WorkOrderStatus.InProgress] = "- - - - - OnHold - Completed Cancelled - -",
        [WorkOrderStatus.OnHold] = "- - - - - - InProgress - Cancelled - -",
        [WorkOrderStatus.Completed] = "- - - - - - - - - Invoiced -",
        [WorkOrderStatus.Invoiced] = "- - - - - - - - - - Completed",
        [WorkOrderStatus.Cancelled] = "- - - - - - - - - - -",
    };

    public static TheoryData<WorkOrderStatus, WorkOrderAction, WorkOrderStatus?> TransitionTable()
    {
        var data = new TheoryData<WorkOrderStatus, WorkOrderAction, WorkOrderStatus?>();
        foreach (var (from, row) in Table)
        {
            var cells = row.Split(' ');
            foreach (var action in Enum.GetValues<WorkOrderAction>())
            {
                var cell = cells[(int)action];
                data.Add(from, action, cell == "-" ? null : Enum.Parse<WorkOrderStatus>(cell));
            }
        }
        return data;
    }

    [Fact]
    public void The_table_covers_every_status_and_action()
    {
        Table.Keys.ShouldBe(Enum.GetValues<WorkOrderStatus>(), ignoreOrder: true);
        Table.Values.ShouldAllBe(row => row.Split(' ', StringSplitOptions.None).Length == Enum.GetValues<WorkOrderAction>().Length);
    }

    [Theory]
    [MemberData(nameof(TransitionTable))]
    public void Every_transition_follows_the_table(WorkOrderStatus from, WorkOrderAction action, WorkOrderStatus? expected)
    {
        var wo = In(from);
        wo.Status.ShouldBe(from);
        var historyBefore = wo.History.Count;

        var result = Act(wo, action);

        if (expected is { } to)
        {
            result.IsSuccess.ShouldBeTrue();
            wo.Status.ShouldBe(to);
            var row = wo.History[^1];
            wo.History.Count.ShouldBe(historyBefore + 1);
            row.FromStatus.ShouldBe(from);
            row.ToStatus.ShouldBe(to);
            row.ChangedBy.ShouldBe(User);
            row.ChangedAt.ShouldBe(Now);
        }
        else
        {
            result.Error.Code.ShouldBe("WorkOrder.InvalidTransition");
            result.Error.Type.ShouldBe(FieldOps.Domain.Common.ErrorType.Conflict);
            wo.Status.ShouldBe(from);
            wo.History.Count.ShouldBe(historyBefore);
        }
    }

    [Fact]
    public void Creating_writes_a_history_row_from_nothing_to_New()
    {
        var wo = NewWorkOrder();

        wo.Status.ShouldBe(WorkOrderStatus.New);
        var row = wo.History.ShouldHaveSingleItem();
        row.FromStatus.ShouldBeNull();
        row.ToStatus.ShouldBe(WorkOrderStatus.New);
    }

    [Fact]
    public void Urgent_work_without_a_due_time_is_due_in_four_hours()
    {
        var wo = WorkOrder.Create("WO-1", Guid.NewGuid(), Guid.NewGuid(), Details(WorkOrderPriority.Urgent), [], User, Now);

        wo.DueBy.ShouldBe(Now.AddHours(4));
    }

    [Fact]
    public void Urgent_work_keeps_a_given_due_time_and_other_priorities_stay_empty()
    {
        var due = Now.AddDays(1);
        WorkOrder.Create("WO-1", Guid.NewGuid(), Guid.NewGuid(), Details(WorkOrderPriority.Urgent, due), [], User, Now).DueBy.ShouldBe(due);
        WorkOrder.Create("WO-2", Guid.NewGuid(), Guid.NewGuid(), Details(WorkOrderPriority.High), [], User, Now).DueBy.ShouldBeNull();
    }

    [Fact]
    public void Scheduling_sets_the_technician_and_times_and_unassigning_clears_them()
    {
        var wo = In(WorkOrderStatus.Scheduled);
        wo.AssignedTechnicianId.ShouldBe(Tech);
        wo.ScheduledStart.ShouldBe(Now.AddHours(1));

        wo.Unassign(User, Now);

        wo.AssignedTechnicianId.ShouldBeNull();
        wo.ScheduledStart.ShouldBeNull();
        wo.ScheduledEnd.ShouldBeNull();
    }

    [Fact]
    public void Schedule_end_must_be_after_start()
    {
        var wo = NewWorkOrder();

        wo.Schedule(Tech, Now.AddHours(2), Now.AddHours(2), User, Now).Error.ShouldBe(WorkOrderErrors.InvalidSchedule);
        wo.Status.ShouldBe(WorkOrderStatus.New);
    }

    [Fact]
    public void Starting_records_the_first_start_time_only()
    {
        var wo = In(WorkOrderStatus.InProgress);
        wo.Hold("Parts", User, Now.AddHours(1));
        wo.Resume(User, Now.AddHours(2));

        wo.StartedAt.ShouldBe(Now);
    }

    [Fact]
    public void En_route_records_the_coordinates()
    {
        var wo = In(WorkOrderStatus.Dispatched);

        wo.EnRoute(User, Now, 25.2, 55.3);

        wo.History[^1].Latitude.ShouldBe(25.2);
        wo.History[^1].Longitude.ShouldBe(55.3);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("  ")]
    public void Hold_needs_a_note(string? note)
    {
        var wo = In(WorkOrderStatus.InProgress);

        wo.Hold(note, User, Now).Error.ShouldBe(WorkOrderErrors.NoteRequired);
        wo.Status.ShouldBe(WorkOrderStatus.InProgress);
    }

    [Fact]
    public void Hold_note_is_kept_in_the_history()
    {
        var wo = In(WorkOrderStatus.OnHold);

        wo.History[^1].Note.ShouldBe("Waiting for parts");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Cancel_needs_a_reason(string? reason)
    {
        var wo = NewWorkOrder();

        wo.Cancel(reason, User, Now).Error.ShouldBe(WorkOrderErrors.ReasonRequired);
        wo.Status.ShouldBe(WorkOrderStatus.New);
    }

    [Fact]
    public void Cancel_stores_the_reason()
    {
        var wo = In(WorkOrderStatus.Cancelled);

        wo.CancelReason.ShouldBe("Customer called off");
        wo.History[^1].Note.ShouldBe("Customer called off");
    }

    [Theory]
    [InlineData(null, "Sara", true)]
    [InlineData("Done", null, true)]
    [InlineData("Done", "Sara", false)]
    public void Complete_needs_notes_signer_and_signature(string? notes, string? signer, bool hasSignature)
    {
        var wo = In(WorkOrderStatus.InProgress);

        wo.Complete(notes, signer, hasSignature ? Guid.NewGuid() : null, User, Now).Error
            .ShouldBe(WorkOrderErrors.CompletionDetailsRequired);
        wo.Status.ShouldBe(WorkOrderStatus.InProgress);
    }

    [Fact]
    public void Completing_records_the_time_and_signature()
    {
        var wo = In(WorkOrderStatus.Completed);

        wo.CompletedAt.ShouldBe(Now);
        wo.SignedByName.ShouldBe("Sara M.");
        wo.CompletionNotes.ShouldBe("Replaced capacitor");
    }

    [Theory]
    [InlineData(WorkOrderStatus.New, true)]
    [InlineData(WorkOrderStatus.Scheduled, true)]
    [InlineData(WorkOrderStatus.Dispatched, true)]
    [InlineData(WorkOrderStatus.OnHold, true)]
    [InlineData(WorkOrderStatus.EnRoute, false)]
    [InlineData(WorkOrderStatus.InProgress, false)]
    [InlineData(WorkOrderStatus.Completed, false)]
    [InlineData(WorkOrderStatus.Invoiced, false)]
    [InlineData(WorkOrderStatus.Cancelled, false)]
    public void Editing_is_allowed_only_before_work_starts_or_while_on_hold(WorkOrderStatus status, bool allowed)
    {
        var wo = In(status);

        var result = wo.Update(Details() with { Title = "Changed" });

        result.IsSuccess.ShouldBe(allowed);
        if (!allowed) result.Error.ShouldBe(WorkOrderErrors.NotEditable);
        wo.Title.ShouldBe(allowed ? "Changed" : "AC not cooling");
    }

    [Theory]
    [InlineData(WorkOrderStatus.New, true)]
    [InlineData(WorkOrderStatus.InProgress, true)]
    [InlineData(WorkOrderStatus.Completed, false)]
    [InlineData(WorkOrderStatus.Invoiced, false)]
    [InlineData(WorkOrderStatus.Cancelled, false)]
    public void Open_means_not_completed_invoiced_or_cancelled(WorkOrderStatus status, bool open)
    {
        In(status).IsOpen.ShouldBe(open);
    }

    [Fact]
    public void Tasks_are_numbered_in_order()
    {
        var wo = NewWorkOrder("One", "Two");
        wo.AddTask("Three");

        wo.Tasks.Select(t => (t.SortOrder, t.Description)).ShouldBe([(1, "One"), (2, "Two"), (3, "Three")]);
    }

    [Fact]
    public void Removing_a_task_closes_the_gap()
    {
        var wo = NewWorkOrder("One", "Two", "Three");

        wo.RemoveTask(wo.Tasks[1].Id);

        wo.Tasks.OrderBy(t => t.SortOrder).Select(t => (t.SortOrder, t.Description)).ShouldBe([(1, "One"), (2, "Three")]);
    }

    [Fact]
    public void Reordering_needs_every_task_once()
    {
        var wo = NewWorkOrder("One", "Two", "Three");
        var ids = wo.Tasks.Select(t => t.Id).ToList();

        wo.ReorderTasks([ids[2], ids[0]]).Error.ShouldBe(WorkOrderErrors.InvalidTaskOrder);
        wo.ReorderTasks([ids[2], ids[0], ids[0]]).Error.ShouldBe(WorkOrderErrors.InvalidTaskOrder);
        wo.ReorderTasks([ids[2], ids[0], ids[1]]).IsSuccess.ShouldBeTrue();

        wo.Tasks.OrderBy(t => t.SortOrder).Select(t => t.Description).ShouldBe(["Three", "One", "Two"]);
    }

    [Fact]
    public void Toggling_records_who_and_when_and_untoggling_clears_it()
    {
        var wo = NewWorkOrder("One");
        var taskId = wo.Tasks[0].Id;

        var task = wo.ToggleTask(taskId, Tech, Now).Value;
        task.IsDone.ShouldBeTrue();
        task.DoneBy.ShouldBe(Tech);
        task.DoneAt.ShouldBe(Now);

        wo.ToggleTask(taskId, Tech, Now);
        task.IsDone.ShouldBeFalse();
        task.DoneBy.ShouldBeNull();
        task.DoneAt.ShouldBeNull();
    }

    [Theory]
    [InlineData(WorkOrderStatus.Completed)]
    [InlineData(WorkOrderStatus.Invoiced)]
    [InlineData(WorkOrderStatus.Cancelled)]
    public void Tasks_cannot_change_once_the_job_is_closed(WorkOrderStatus status)
    {
        var wo = In(status);
        var taskId = wo.Tasks[0].Id;

        wo.AddTask("More").Error.ShouldBe(WorkOrderErrors.TasksLocked);
        wo.UpdateTask(taskId, "X").Error.ShouldBe(WorkOrderErrors.TasksLocked);
        wo.RemoveTask(taskId).Error.ShouldBe(WorkOrderErrors.TasksLocked);
        wo.ReorderTasks([taskId]).Error.ShouldBe(WorkOrderErrors.TasksLocked);
        wo.ToggleTask(taskId, User, Now).Error.ShouldBe(WorkOrderErrors.TasksLocked);
    }

    [Fact]
    public void Unknown_task_returns_not_found()
    {
        NewWorkOrder().ToggleTask(Guid.NewGuid(), User, Now).Error.ShouldBe(WorkOrderErrors.TaskNotFound);
    }
}
