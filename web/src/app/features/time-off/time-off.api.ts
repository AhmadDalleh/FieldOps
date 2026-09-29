import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

export type TimeOffStatus = 'Pending' | 'Approved' | 'Rejected';

export interface TimeOff {
  id: string;
  technicianId: string;
  technicianName: string;
  startsAt: string;
  endsAt: string;
  reason: string | null;
  status: TimeOffStatus;
  createdAt: string;
}

export interface TimeOffInput {
  startsAt: string;
  endsAt: string;
  reason: string | null;
  /** Required for office staff; ignored for technicians, who always request for themselves. */
  technicianId?: string | null;
}

export interface ConflictingJob {
  workOrderId: string;
  number: string;
  scheduledStart: string;
  scheduledEnd: string;
}

export interface TimeOffDecision {
  timeOff: TimeOff;
  conflictingJobs: ConflictingJob[];
}

@Injectable({ providedIn: 'root' })
export class TimeOffApi {
  private readonly http = inject(HttpClient);

  list(status: TimeOffStatus | null = null): Observable<TimeOff[]> {
    let params = new HttpParams();
    if (status) params = params.set('status', status);
    return this.http.get<TimeOff[]>('/api/time-off', { params });
  }

  request(input: TimeOffInput): Observable<TimeOff> {
    return this.http.post<TimeOff>('/api/time-off', input);
  }

  decide(id: string, approve: boolean): Observable<TimeOffDecision> {
    return this.http.post<TimeOffDecision>(`/api/time-off/${id}/${approve ? 'approve' : 'reject'}`, null);
  }
}
