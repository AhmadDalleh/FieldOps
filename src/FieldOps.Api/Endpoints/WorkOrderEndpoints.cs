using FieldOps.Api.Common;
using FieldOps.Application.Common;
using FieldOps.Application.Features.WorkOrders;
using FieldOps.Domain.Common;
using FieldOps.Domain.WorkOrders;
using Microsoft.AspNetCore.Mvc;

namespace FieldOps.Api.Endpoints;

public static class WorkOrderEndpoints
{
    public static IEndpointRouteBuilder MapWorkOrderEndpoints(this IEndpointRouteBuilder app)
    {
        // Routes open to any signed-in user limit technicians to their own jobs inside the handler ("T*").
        var group = app.MapGroup("/api/work-orders").WithTags("Work orders").RequireAuthorization();

        group.MapGet("/", async (int? page, int? pageSize, string? search, string? sort, string? status,
                WorkOrderPriority? priority, WorkOrderType? type, Guid? technicianId, Guid? customerId, Guid? siteId,
                DateOnly? from, DateOnly? to, ListWorkOrdersHandler handler, CancellationToken ct) =>
            {
                if (!TryParseStatuses(status, out var statuses))
                    return Error.Validation("WorkOrder.InvalidStatusFilter", $"Unknown status in '{status}'.").ToProblem();
                return Results.Ok(await handler.Handle(new ListWorkOrdersQuery(
                    new PageRequest(page ?? 1, pageSize ?? 20, search, sort),
                    statuses, priority, type, technicianId, customerId, siteId, from, to), ct));
            })
            .RequireAuthorization(Policies.OfficeStaff);

        group.MapPost("/", async (CreateWorkOrderInput input, CreateWorkOrderHandler handler, CancellationToken ct) =>
                (await handler.Handle(new CreateWorkOrderCommand(input), ct))
                    .ToHttp(wo => Results.Created($"/api/work-orders/{wo.Id}", wo)))
            .Validate<CreateWorkOrderInput>()
            .RequireAuthorization(Policies.OfficeStaff);

        group.MapGet("/{id:guid}", async (Guid id, GetWorkOrderHandler handler, CancellationToken ct) =>
            (await handler.Handle(new GetWorkOrderQuery(id), ct)).ToHttp());

        group.MapPut("/{id:guid}", async (Guid id, UpdateWorkOrderInput input, UpdateWorkOrderHandler handler, CancellationToken ct) =>
                (await handler.Handle(new UpdateWorkOrderCommand(id, input), ct)).ToHttp())
            .Validate<UpdateWorkOrderInput>()
            .RequireAuthorization(Policies.OfficeStaff);

        group.MapGet("/{id:guid}/history", async (Guid id, GetWorkOrderHistoryHandler handler, CancellationToken ct) =>
            (await handler.Handle(new GetWorkOrderHistoryQuery(id), ct)).ToHttp());

        // Scheduling and dispatch
        group.MapPost("/{id:guid}/schedule", async (Guid id, ScheduleInput input, ScheduleWorkOrderHandler handler, CancellationToken ct) =>
                (await handler.Handle(new ScheduleWorkOrderCommand(id, input), ct)).ToHttp())
            .Validate<ScheduleInput>()
            .RequireAuthorization(Policies.OfficeStaff);

        group.MapPost("/{id:guid}/unassign", async (Guid id, UnassignWorkOrderHandler handler, CancellationToken ct) =>
                (await handler.Handle(new UnassignWorkOrderCommand(id), ct)).ToHttp())
            .RequireAuthorization(Policies.OfficeStaff);

        group.MapPost("/{id:guid}/dispatch", async (Guid id, DispatchWorkOrderHandler handler, CancellationToken ct) =>
                (await handler.Handle(new DispatchWorkOrderCommand(id), ct)).ToHttp())
            .RequireAuthorization(Policies.OfficeStaff);

        group.MapPost("/dispatch-day", async (DispatchDayInput input, DispatchDayHandler handler, CancellationToken ct) =>
                (await handler.Handle(new DispatchDayCommand(input), ct)).ToHttp())
            .RequireAuthorization(Policies.OfficeStaff);

        // Status workflow
        group.MapPost("/{id:guid}/hold", async (Guid id, HoldInput input, HoldWorkOrderHandler handler, CancellationToken ct) =>
            (await handler.Handle(new HoldWorkOrderCommand(id, input), ct)).ToHttp());

        group.MapPost("/{id:guid}/resume", async (Guid id, ResumeWorkOrderHandler handler, CancellationToken ct) =>
            (await handler.Handle(new ResumeWorkOrderCommand(id), ct)).ToHttp());

        // Field work by the assigned technician (US-TAPP-03/04/08)
        group.MapPost("/{id:guid}/en-route", async (Guid id, LocationInput input, EnRouteHandler handler, CancellationToken ct) =>
                (await handler.Handle(new EnRouteCommand(id, input), ct)).ToHttp())
            .Validate<LocationInput>();

        group.MapPost("/{id:guid}/start", async (Guid id, LocationInput input, StartWorkOrderHandler handler, CancellationToken ct) =>
                (await handler.Handle(new StartWorkOrderCommand(id, input), ct)).ToHttp())
            .Validate<LocationInput>();

        group.MapPost("/{id:guid}/complete", async (Guid id, CompleteInput input, CompleteWorkOrderHandler handler, CancellationToken ct) =>
                (await handler.Handle(new CompleteWorkOrderCommand(id, input), ct)).ToHttp())
            .Validate<CompleteInput>();

        group.MapPost("/{id:guid}/cancel", async (Guid id, CancelInput input, CancelWorkOrderHandler handler, CancellationToken ct) =>
                (await handler.Handle(new CancelWorkOrderCommand(id, input), ct)).ToHttp())
            .RequireAuthorization(Policies.OfficeStaff);

        // Tasks
        group.MapPost("/{id:guid}/tasks", async (Guid id, TaskInput input, AddTaskHandler handler, CancellationToken ct) =>
                (await handler.Handle(new AddTaskCommand(id, input), ct)).ToHttp())
            .Validate<TaskInput>()
            .RequireAuthorization(Policies.OfficeStaff);

        group.MapPut("/{id:guid}/tasks/{taskId:guid}", async (Guid id, Guid taskId, TaskInput input, UpdateTaskHandler handler, CancellationToken ct) =>
                (await handler.Handle(new UpdateTaskCommand(id, taskId, input), ct)).ToHttp())
            .Validate<TaskInput>()
            .RequireAuthorization(Policies.OfficeStaff);

        group.MapDelete("/{id:guid}/tasks/{taskId:guid}", async (Guid id, Guid taskId, RemoveTaskHandler handler, CancellationToken ct) =>
                (await handler.Handle(new RemoveTaskCommand(id, taskId), ct)).ToHttp())
            .RequireAuthorization(Policies.OfficeStaff);

        group.MapPost("/{id:guid}/tasks/reorder", async (Guid id, ReorderTasksInput input, ReorderTasksHandler handler, CancellationToken ct) =>
                (await handler.Handle(new ReorderTasksCommand(id, input), ct)).ToHttp())
            .RequireAuthorization(Policies.OfficeStaff);

        group.MapPost("/{id:guid}/tasks/{taskId:guid}/toggle", async (Guid id, Guid taskId, ToggleTaskHandler handler, CancellationToken ct) =>
            (await handler.Handle(new ToggleTaskCommand(id, taskId), ct)).ToHttp());

        // Notes
        group.MapGet("/{id:guid}/notes", async (Guid id, ListNotesHandler handler, CancellationToken ct) =>
            (await handler.Handle(new ListNotesQuery(id), ct)).ToHttp());

        group.MapPost("/{id:guid}/notes", async (Guid id, NoteInput input, AddNoteHandler handler, CancellationToken ct) =>
                (await handler.Handle(new AddNoteCommand(id, input), ct))
                    .ToHttp(note => Results.Created($"/api/work-orders/{id}/notes/{note.Id}", note)))
            .Validate<NoteInput>();

        group.MapPut("/{id:guid}/notes/{noteId:guid}", async (Guid id, Guid noteId, NoteInput input, EditNoteHandler handler, CancellationToken ct) =>
                (await handler.Handle(new EditNoteCommand(id, noteId, input), ct)).ToHttp())
            .Validate<NoteInput>();

        // Photos, signatures and documents (US-TAPP-06)
        group.MapGet("/{id:guid}/attachments", async (Guid id, ListAttachmentsHandler handler, CancellationToken ct) =>
            (await handler.Handle(new ListAttachmentsQuery(id), ct)).ToHttp());

        group.MapPost("/{id:guid}/attachments", async (Guid id, IFormFile file, [FromForm] AttachmentKind kind,
                UploadAttachmentHandler handler, CancellationToken ct) =>
            {
                await using var stream = file.OpenReadStream();
                return (await handler.Handle(
                        new UploadAttachmentCommand(id, kind, file.FileName, file.ContentType, file.Length, stream), ct))
                    .ToHttp(a => Results.Created($"/api/attachments/{a.Id}", a));
            })
            .DisableAntiforgery();

        // Time log (US-TAPP-09)
        group.MapGet("/{id:guid}/time-entries", async (Guid id, ListTimeEntriesHandler handler, CancellationToken ct) =>
            (await handler.Handle(new ListTimeEntriesQuery(id), ct)).ToHttp());

        var attachments = app.MapGroup("/api/attachments").WithTags("Work orders").RequireAuthorization();

        attachments.MapGet("/{id:guid}", async (Guid id, GetAttachmentFileHandler handler, CancellationToken ct) =>
        {
            var result = await handler.Handle(new GetAttachmentFileQuery(id), ct);
            return result.IsSuccess
                ? Results.Stream(result.Value.Content, result.Value.ContentType, result.Value.FileName)
                : result.Error.ToProblem();
        });

        attachments.MapDelete("/{id:guid}", async (Guid id, DeleteAttachmentHandler handler, CancellationToken ct) =>
            (await handler.Handle(new DeleteAttachmentCommand(id), ct)).ToHttp());

        app.MapGroup("/api/time-entries").WithTags("Work orders").RequireAuthorization()
            .MapPut("/{id:guid}", async (Guid id, TimeEntryInput input, CorrectTimeEntryHandler handler, CancellationToken ct) =>
                (await handler.Handle(new CorrectTimeEntryCommand(id, input), ct)).ToHttp())
            .Validate<TimeEntryInput>();

        return app;
    }

    /// <summary>Parses <c>?status=New,Scheduled</c>.</summary>
    private static bool TryParseStatuses(string? value, out IReadOnlyList<WorkOrderStatus>? statuses)
    {
        statuses = null;
        if (string.IsNullOrWhiteSpace(value)) return true;

        var parsed = new List<WorkOrderStatus>();
        foreach (var part in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!Enum.TryParse<WorkOrderStatus>(part, ignoreCase: true, out var status) || !Enum.IsDefined(status)) return false;
            parsed.Add(status);
        }
        statuses = parsed;
        return true;
    }
}
