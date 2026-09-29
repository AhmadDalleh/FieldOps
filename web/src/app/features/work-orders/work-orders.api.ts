import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { PagedResult } from '../../shared/models/paged-result';

export type WorkOrderStatus =
  | 'New'
  | 'Scheduled'
  | 'Dispatched'
  | 'EnRoute'
  | 'InProgress'
  | 'OnHold'
  | 'Completed'
  | 'Invoiced'
  | 'Cancelled';
export type WorkOrderPriority = 'Low' | 'Medium' | 'High' | 'Urgent';
export type WorkOrderType = 'Repair' | 'Installation' | 'Maintenance' | 'Inspection';
export type WorkOrderAction =
  | 'Schedule'
  | 'Unassign'
  | 'Dispatch'
  | 'EnRoute'
  | 'Start'
  | 'Hold'
  | 'Resume'
  | 'Complete'
  | 'Cancel'
  | 'Invoice'
  | 'VoidInvoice';

export const STATUSES: WorkOrderStatus[] = [
  'New', 'Scheduled', 'Dispatched', 'EnRoute', 'InProgress', 'OnHold', 'Completed', 'Invoiced', 'Cancelled',
];
export const OPEN_STATUSES: WorkOrderStatus[] = ['New', 'Scheduled', 'Dispatched', 'EnRoute', 'InProgress', 'OnHold'];
export const PRIORITIES: WorkOrderPriority[] = ['Urgent', 'High', 'Medium', 'Low'];
export const TYPES: WorkOrderType[] = ['Repair', 'Installation', 'Maintenance', 'Inspection'];

export interface WorkOrderTask {
  id: string;
  sortOrder: number;
  description: string;
  isDone: boolean;
  doneAt: string | null;
  doneByName: string | null;
}

export interface WorkOrder {
  id: string;
  number: string;
  status: WorkOrderStatus;
  title: string;
  description: string | null;
  type: WorkOrderType;
  priority: WorkOrderPriority;
  dueBy: string | null;
  isOverdue: boolean;
  scheduledStart: string | null;
  scheduledEnd: string | null;
  customer: { id: string; code: string; name: string; phone: string };
  site: {
    id: string;
    name: string;
    addressLine1: string;
    addressLine2: string | null;
    city: string;
    latitude: number | null;
    longitude: number | null;
    accessNotes: string | null;
  };
  asset: {
    id: string;
    name: string;
    assetType: string;
    manufacturer: string | null;
    model: string | null;
    serialNumber: string | null;
    warrantyExpiresOn: string | null;
    underWarranty: boolean;
  } | null;
  technician: { id: string; name: string; color: string } | null;
  requiredSkill: { id: string; name: string } | null;
  startedAt: string | null;
  completedAt: string | null;
  completionNotes: string | null;
  signedByName: string | null;
  cancelReason: string | null;
  tasks: WorkOrderTask[];
  allowedActions: WorkOrderAction[];
  isEditable: boolean;
  createdAt: string;
  version: number;
}

export interface WorkOrderListItem {
  id: string;
  number: string;
  title: string;
  status: WorkOrderStatus;
  type: WorkOrderType;
  priority: WorkOrderPriority;
  customerName: string;
  siteName: string;
  technicianName: string | null;
  dueBy: string | null;
  scheduledStart: string | null;
  isOverdue: boolean;
  createdAt: string;
}

export interface WorkOrderQuery {
  page: number;
  pageSize: number;
  search: string;
  statuses: WorkOrderStatus[];
  priority: WorkOrderPriority | null;
  type: WorkOrderType | null;
  customerId?: string | null;
}

export interface CreateWorkOrderInput {
  customerId: string;
  siteId: string;
  assetId: string | null;
  title: string;
  description: string | null;
  type: WorkOrderType;
  priority: WorkOrderPriority;
  dueBy: string | null;
  requiredSkillId: string | null;
}

export interface UpdateWorkOrderInput {
  title: string;
  description: string | null;
  type: WorkOrderType;
  priority: WorkOrderPriority;
  dueBy: string | null;
  assetId: string | null;
  requiredSkillId: string | null;
  version: number;
}

export interface ScheduleInput {
  technicianId: string;
  start: string;
  end: string;
  allowOverlap?: boolean;
}

export interface ScheduledJob {
  workOrderId: string;
  number: string;
  scheduledStart: string;
  scheduledEnd: string;
}

export interface ScheduleResult {
  workOrder: WorkOrder;
  warnings: string[];
}

export interface HistoryItem {
  fromStatus: WorkOrderStatus | null;
  toStatus: WorkOrderStatus;
  changedByName: string;
  changedAt: string;
  note: string | null;
}

export interface Note {
  id: string;
  authorId: string;
  authorName: string;
  body: string;
  isInternal: boolean;
  createdAt: string;
  updatedAt: string | null;
  canEdit: boolean;
}

@Injectable({ providedIn: 'root' })
export class WorkOrdersApi {
  private readonly http = inject(HttpClient);

  list(q: WorkOrderQuery): Observable<PagedResult<WorkOrderListItem>> {
    let params = new HttpParams().set('page', q.page).set('pageSize', q.pageSize);
    if (q.search) params = params.set('search', q.search);
    if (q.statuses.length) params = params.set('status', q.statuses.join(','));
    if (q.priority) params = params.set('priority', q.priority);
    if (q.type) params = params.set('type', q.type);
    if (q.customerId) params = params.set('customerId', q.customerId);
    return this.http.get<PagedResult<WorkOrderListItem>>('/api/work-orders', { params });
  }

  get(id: string): Observable<WorkOrder> {
    return this.http.get<WorkOrder>(`/api/work-orders/${id}`);
  }

  create(input: CreateWorkOrderInput): Observable<WorkOrder> {
    return this.http.post<WorkOrder>('/api/work-orders', input);
  }

  update(id: string, input: UpdateWorkOrderInput): Observable<WorkOrder> {
    return this.http.put<WorkOrder>(`/api/work-orders/${id}`, input);
  }

  schedule(id: string, input: ScheduleInput): Observable<ScheduleResult> {
    return this.http.post<ScheduleResult>(`/api/work-orders/${id}/schedule`, input);
  }

  unassign(id: string): Observable<WorkOrder> {
    return this.http.post<WorkOrder>(`/api/work-orders/${id}/unassign`, null);
  }

  dispatch(id: string): Observable<WorkOrder> {
    return this.http.post<WorkOrder>(`/api/work-orders/${id}/dispatch`, null);
  }

  dispatchDay(technicianId: string, date: string): Observable<{ dispatched: string[] }> {
    return this.http.post<{ dispatched: string[] }>('/api/work-orders/dispatch-day', { technicianId, date });
  }

  history(id: string): Observable<HistoryItem[]> {
    return this.http.get<HistoryItem[]>(`/api/work-orders/${id}/history`);
  }

  hold(id: string, note: string): Observable<WorkOrder> {
    return this.http.post<WorkOrder>(`/api/work-orders/${id}/hold`, { note });
  }

  resume(id: string): Observable<WorkOrder> {
    return this.http.post<WorkOrder>(`/api/work-orders/${id}/resume`, {});
  }

  cancel(id: string, reason: string): Observable<WorkOrder> {
    return this.http.post<WorkOrder>(`/api/work-orders/${id}/cancel`, { reason });
  }

  addTask(id: string, description: string): Observable<WorkOrder> {
    return this.http.post<WorkOrder>(`/api/work-orders/${id}/tasks`, { description });
  }

  updateTask(id: string, taskId: string, description: string): Observable<WorkOrder> {
    return this.http.put<WorkOrder>(`/api/work-orders/${id}/tasks/${taskId}`, { description });
  }

  removeTask(id: string, taskId: string): Observable<WorkOrder> {
    return this.http.delete<WorkOrder>(`/api/work-orders/${id}/tasks/${taskId}`);
  }

  reorderTasks(id: string, taskIds: string[]): Observable<WorkOrder> {
    return this.http.post<WorkOrder>(`/api/work-orders/${id}/tasks/reorder`, { taskIds });
  }

  toggleTask(id: string, taskId: string): Observable<WorkOrder> {
    return this.http.post<WorkOrder>(`/api/work-orders/${id}/tasks/${taskId}/toggle`, null);
  }

  notes(id: string): Observable<Note[]> {
    return this.http.get<Note[]>(`/api/work-orders/${id}/notes`);
  }

  addNote(id: string, body: string, isInternal: boolean): Observable<Note> {
    return this.http.post<Note>(`/api/work-orders/${id}/notes`, { body, isInternal });
  }

  editNote(id: string, noteId: string, body: string, isInternal: boolean): Observable<Note> {
    return this.http.put<Note>(`/api/work-orders/${id}/notes/${noteId}`, { body, isInternal });
  }
}
