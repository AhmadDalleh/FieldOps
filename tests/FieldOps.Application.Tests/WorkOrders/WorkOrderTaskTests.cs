using FieldOps.Application.Features.WorkOrders;
using FieldOps.Application.Tests.Infrastructure;
using FieldOps.Domain.Common;
using FieldOps.Domain.Identity;
using FieldOps.Domain.WorkOrders;
using Shouldly;

namespace FieldOps.Application.Tests.WorkOrders;

public class WorkOrderTaskTests(PostgresFixture fixture) : TestBase(fixture)
{
    private async Task<WorkOrderDto> GivenWorkOrderWithTasks(params string[] tasks)
    {
        var (c, s) = await GivenCustomerAndSite();
        var wo = await GivenWorkOrder(c, s);
        foreach (var task in tasks) wo = (await AddTask(wo.Id, task)).Value;
        return wo;
    }

    private Task<Result<WorkOrderDto>> AddTask(Guid id, string description) =>
        Resolve<AddTaskHandler>().Handle(new AddTaskCommand(id, new TaskInput(description)), default);

    private Task<Result<WorkOrderDto>> Toggle(Guid id, Guid taskId) =>
        Resolve<ToggleTaskHandler>().Handle(new ToggleTaskCommand(id, taskId), default);

    [Fact]
    public async Task Office_adds_edits_reorders_and_removes_tasks()
    {
        var wo = await GivenWorkOrderWithTasks("Isolate power", "Replace capacitor", "Test run");
        var ids = wo.Tasks.Select(t => t.Id).ToList();

        wo = (await Resolve<UpdateTaskHandler>().Handle(new UpdateTaskCommand(wo.Id, ids[1], new TaskInput("Replace run capacitor")), default)).Value;
        wo = (await Resolve<ReorderTasksHandler>().Handle(new ReorderTasksCommand(wo.Id, new ReorderTasksInput([ids[2], ids[0], ids[1]])), default)).Value;
        wo.Tasks.Select(t => t.Description).ShouldBe(["Test run", "Isolate power", "Replace run capacitor"]);

        wo = (await Resolve<RemoveTaskHandler>().Handle(new RemoveTaskCommand(wo.Id, ids[0]), default)).Value;

        wo.Tasks.Select(t => (t.SortOrder, t.Description)).ShouldBe([(1, "Test run"), (2, "Replace run capacitor")]);
    }

    [Fact]
    public async Task Assigned_technician_ticks_a_task_and_it_records_who_and_when()
    {
        var wo = await GivenWorkOrderWithTasks("Check filter");
        var (techUser, tech) = await GivenTechnician();
        await Assign(wo.Id, tech);
        Fixture.CurrentUser.SignInAs(techUser, Role.Technician, tech);

        var task = (await Toggle(wo.Id, wo.Tasks[0].Id)).Value.Tasks.Single();

        task.IsDone.ShouldBeTrue();
        task.DoneByName.ShouldBe("Technician User");
        task.DoneAt.ShouldBe(PostgresFixture.Start);

        (await Toggle(wo.Id, wo.Tasks[0].Id)).Value.Tasks.Single().DoneAt.ShouldBeNull();
    }

    [Fact]
    public async Task Technician_cannot_tick_tasks_on_someone_elses_job()
    {
        var wo = await GivenWorkOrderWithTasks("Check filter");
        var (_, assigned) = await GivenTechnician();
        await Assign(wo.Id, assigned);
        var (otherUser, other) = await GivenTechnician();
        Fixture.CurrentUser.SignInAs(otherUser, Role.Technician, other);

        (await Toggle(wo.Id, wo.Tasks[0].Id)).Error.ShouldBe(Application.Common.Errors.Forbidden);
    }

    [Fact]
    public async Task Tasks_cannot_change_after_completion()
    {
        var wo = await GivenWorkOrderWithTasks("Check filter");
        var (_, tech) = await GivenTechnician();
        await Complete(wo.Id, tech);

        (await AddTask(wo.Id, "More")).Error.ShouldBe(WorkOrderErrors.TasksLocked);
        (await Toggle(wo.Id, wo.Tasks[0].Id)).Error.ShouldBe(WorkOrderErrors.TasksLocked);
    }

    [Fact]
    public async Task Reorder_must_name_every_task()
    {
        var wo = await GivenWorkOrderWithTasks("One", "Two");

        (await Resolve<ReorderTasksHandler>().Handle(new ReorderTasksCommand(wo.Id, new ReorderTasksInput([wo.Tasks[0].Id])), default))
            .Error.ShouldBe(WorkOrderErrors.InvalidTaskOrder);
    }

    [Fact]
    public async Task Unknown_task_returns_not_found()
    {
        var wo = await GivenWorkOrderWithTasks();

        (await Toggle(wo.Id, Guid.NewGuid())).Error.ShouldBe(WorkOrderErrors.TaskNotFound);
    }
}
