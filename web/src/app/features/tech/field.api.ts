import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { WorkOrder, WorkOrderPriority, WorkOrderStatus, WorkOrderType } from '../work-orders/work-orders.api';

export type JobDay = 'Today' | 'Tomorrow';
export type AttachmentKind = 'Photo' | 'Document' | 'Signature';

export interface MyJob {
  id: string;
  number: string;
  title: string;
  status: WorkOrderStatus;
  priority: WorkOrderPriority;
  type: WorkOrderType;
  scheduledStart: string | null;
  scheduledEnd: string | null;
  customerName: string;
  siteName: string;
  siteAddress: string;
  siteCity: string;
}

export interface Location {
  lat: number;
  lng: number;
}

export interface CompleteInput {
  completionNotes: string;
  signedByName: string;
  signatureAttachmentId: string;
  skippedTasksReason: string | null;
}

export interface Attachment {
  id: string;
  kind: AttachmentKind;
  fileName: string;
  contentType: string;
  sizeBytes: number;
  uploadedByName: string;
  uploadedAt: string;
  canDelete: boolean;
}

export interface TimeEntry {
  id: string;
  technicianId: string;
  technicianName: string;
  type: 'Travel' | 'Work';
  startedAt: string;
  endedAt: string | null;
  durationMinutes: number | null;
  canEdit: boolean;
}

/** The technician app's calls: field actions, photos and signatures, and the time log. */
@Injectable({ providedIn: 'root' })
export class FieldApi {
  private readonly http = inject(HttpClient);

  myJobs(day: JobDay): Observable<MyJob[]> {
    return this.http.get<MyJob[]>('/api/me/jobs', { params: new HttpParams().set('day', day) });
  }

  enRoute(id: string, at: Location | null): Observable<WorkOrder> {
    return this.http.post<WorkOrder>(`/api/work-orders/${id}/en-route`, at ?? {});
  }

  start(id: string, at: Location | null): Observable<WorkOrder> {
    return this.http.post<WorkOrder>(`/api/work-orders/${id}/start`, at ?? {});
  }

  complete(id: string, input: CompleteInput): Observable<WorkOrder> {
    return this.http.post<WorkOrder>(`/api/work-orders/${id}/complete`, input);
  }

  attachments(id: string): Observable<Attachment[]> {
    return this.http.get<Attachment[]>(`/api/work-orders/${id}/attachments`);
  }

  upload(id: string, kind: AttachmentKind, file: Blob, fileName: string): Observable<Attachment> {
    const form = new FormData();
    form.append('file', file, fileName);
    form.append('kind', kind);
    return this.http.post<Attachment>(`/api/work-orders/${id}/attachments`, form);
  }

  deleteAttachment(id: string): Observable<void> {
    return this.http.delete<void>(`/api/attachments/${id}`);
  }

  /** Files need the bearer token, so they are fetched as blobs rather than linked directly. */
  file(id: string): Observable<Blob> {
    return this.http.get(`/api/attachments/${id}`, { responseType: 'blob' });
  }

  timeEntries(id: string): Observable<TimeEntry[]> {
    return this.http.get<TimeEntry[]>(`/api/work-orders/${id}/time-entries`);
  }

  correctTimeEntry(id: string, startedAt: string, endedAt: string | null): Observable<TimeEntry[]> {
    return this.http.put<TimeEntry[]>(`/api/time-entries/${id}`, { startedAt, endedAt });
  }
}
