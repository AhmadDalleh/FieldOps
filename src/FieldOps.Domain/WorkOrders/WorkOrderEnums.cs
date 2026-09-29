namespace FieldOps.Domain.WorkOrders;

public enum WorkOrderStatus { New, Scheduled, Dispatched, EnRoute, InProgress, OnHold, Completed, Invoiced, Cancelled }

public enum WorkOrderPriority { Low, Medium, High, Urgent }

public enum WorkOrderType { Repair, Installation, Maintenance, Inspection }

/// <summary>The actions of the work order state machine (docs/07-flows.md).</summary>
public enum WorkOrderAction { Schedule, Unassign, Dispatch, EnRoute, Start, Hold, Resume, Complete, Cancel, Invoice, VoidInvoice }
