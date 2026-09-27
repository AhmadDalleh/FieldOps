import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { WorkOrderPriority, WorkOrderStatus, WorkOrderType } from '../work-orders/work-orders.api';

export interface BoardTechnician {
  id: string;
  name: string;
  color: string;
  workingHoursStart: string;
  workingHoursEnd: string;
  skills: { id: string; name: string }[];
}

export interface BoardJob {
  id: string;
  number: string;
  title: string;
  status: WorkOrderStatus;
  priority: WorkOrderPriority;
  type: WorkOrderType;
  customerName: string;
  siteName: string;
  siteAddress: string;
  latitude: number | null;
  longitude: number | null;
  technicianId: string | null;
  scheduledStart: string | null;
  scheduledEnd: string | null;
  dueBy: string | null;
  requiredSkillId: string | null;
}

export interface BoardTimeOff {
  id: string;
  technicianId: string;
  startsAt: string;
  endsAt: string;
  reason: string | null;
}

export interface DispatchBoard {
  date: string;
  technicians: BoardTechnician[];
  jobs: BoardJob[];
  timeOff: BoardTimeOff[];
  unassigned: BoardJob[];
}

@Injectable({ providedIn: 'root' })
export class DispatchApi {
  private readonly http = inject(HttpClient);

  board(date: string): Observable<DispatchBoard> {
    return this.http.get<DispatchBoard>('/api/dispatch/board', { params: new HttpParams().set('date', date) });
  }
}
