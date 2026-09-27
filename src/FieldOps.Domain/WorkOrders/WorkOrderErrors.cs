using FieldOps.Domain.Common;

namespace FieldOps.Domain.WorkOrders;

public static class WorkOrderErrors
{
    public static readonly Error NotFound = Error.NotFound("WorkOrder.NotFound", "The work order was not found.");
    public static readonly Error NotEditable = Error.Conflict("WorkOrder.NotEditable",
        "The work order can only be edited while it is New, Scheduled, Dispatched or On hold.");
    public static readonly Error ConcurrencyConflict = Error.Conflict("WorkOrder.ConcurrencyConflict",
        "Someone else changed this work order. Reload it and try again.");
    public static readonly Error TasksLocked = Error.Conflict("WorkOrder.TasksLocked",
        "Tasks cannot change once the work order is completed or cancelled.");
    public static readonly Error TaskNotFound = Error.NotFound("WorkOrder.TaskNotFound", "The task was not found.");
    public static readonly Error InvalidTaskOrder = Error.Validation("WorkOrder.InvalidTaskOrder",
        "The new order must list every task of the work order exactly once.");
    public static readonly Error NoteRequired = Error.Validation("WorkOrder.NoteRequired", "Say why the job is on hold.");
    public static readonly Error ReasonRequired = Error.Validation("WorkOrder.ReasonRequired", "Give a reason for cancelling.");
    public static readonly Error CompletionDetailsRequired = Error.Validation("WorkOrder.CompletionDetailsRequired",
        "Completion notes, the signer's name and a signature are required.");
    public static readonly Error InvalidSchedule = Error.Validation("WorkOrder.InvalidSchedule",
        "The scheduled end must be after the start.");
    public static readonly Error SiteNotOfCustomer = Error.Validation("WorkOrder.SiteNotOfCustomer",
        "The site must be an active site of the selected customer.");
    public static readonly Error CustomerNotAvailable = Error.Validation("WorkOrder.CustomerNotAvailable",
        "The customer was not found or is inactive.");
    public static readonly Error AssetNotOfSite = Error.Validation("WorkOrder.AssetNotOfSite",
        "The asset must belong to the selected site.");
    public static readonly Error NoteNotFound = Error.NotFound("WorkOrder.NoteNotFound", "The note was not found.");
    public static readonly Error NoteNotYours = Error.Forbidden("WorkOrder.NoteNotYours", "Only the author can edit a note.");
    public static readonly Error NoteEditWindowClosed = Error.Conflict("WorkOrder.NoteEditWindowClosed",
        "Notes can only be edited within 15 minutes of being added.");

    public static Error InvalidTransition(WorkOrderStatus from, WorkOrderAction action) =>
        Error.Conflict("WorkOrder.InvalidTransition", $"A work order that is {from} cannot {action}.");
}
