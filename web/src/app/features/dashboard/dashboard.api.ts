import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { WorkOrderPriority, WorkOrderStatus } from '../work-orders/work-orders.api';

export type TechnicianState = 'Busy' | 'Free' | 'Off';

export interface StatusCount {
  status: WorkOrderStatus;
  count: number;
}

export interface DashboardTechnician {
  id: string;
  name: string;
  color: string;
  state: TechnicianState;
  currentJobId: string | null;
  currentJobNumber: string | null;
  jobsToday: number;
}

export interface DashboardJob {
  id: string;
  number: string;
  title: string;
  priority: WorkOrderPriority;
  customerName: string;
  dueBy: string | null;
  isOverdue: boolean;
  createdAt: string;
}

export interface DashboardData {
  date: string;
  openByStatus: StatusCount[];
  unassigned: number;
  overdue: number;
  completedToday: number;
  techniciansBusy: number;
  techniciansFree: number;
  techniciansOff: number;
  technicians: DashboardTechnician[];
  urgentUnassigned: DashboardJob[];
}

/** Each status's share of the open jobs, in percent, for the pipeline bar; empty when nothing is open. */
export function statusShares(counts: StatusCount[]): { status: WorkOrderStatus; count: number; percent: number }[] {
  const total = counts.reduce((sum, c) => sum + c.count, 0);
  if (total === 0) return [];
  return counts.filter((c) => c.count > 0).map((c) => ({ ...c, percent: (c.count / total) * 100 }));
}

@Injectable({ providedIn: 'root' })
export class DashboardApi {
  private readonly http = inject(HttpClient);

  today(): Observable<DashboardData> {
    return this.http.get<DashboardData>('/api/dashboard/today');
  }
}
