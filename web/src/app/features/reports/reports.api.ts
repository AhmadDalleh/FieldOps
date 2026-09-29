import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { fileNameFrom } from '../../shared/files/download';

export type ReportKind = 'technicians' | 'revenue' | 'parts-usage';
export type RevenueGrouping = 'Month' | 'Customer';

export interface ReportFilter {
  from: string;
  to: string;
  groupBy: RevenueGrouping;
}

export interface TechnicianReportRow {
  technicianId: string;
  name: string;
  employeeCode: string;
  completedJobs: number;
  averageMinutes: number | null;
  workHours: number;
}

export interface TechnicianReport {
  from: string;
  to: string;
  rows: TechnicianReportRow[];
  completedJobs: number;
  averageMinutes: number | null;
  workHours: number;
}

export interface RevenueRow {
  key: string;
  label: string;
  invoices: number;
  subtotal: number;
  vat: number;
  total: number;
  paid: number;
  outstanding: number;
}

export interface RevenueReport {
  from: string;
  to: string;
  groupBy: RevenueGrouping;
  rows: RevenueRow[];
  totals: RevenueRow;
}

export interface PartsUsageRow {
  partId: string;
  sku: string;
  name: string;
  unit: string;
  quantity: number;
  jobs: number;
  cost: number;
  price: number;
  margin: number;
  marginPercent: number | null;
}

export interface PartsUsageReport {
  from: string;
  to: string;
  rows: PartsUsageRow[];
  cost: number;
  price: number;
  margin: number;
  marginPercent: number | null;
}

/** The first of the month of a `yyyy-MM-dd` date. */
export function monthStart(day: string): string {
  return `${day.slice(0, 8)}01`;
}

/** Minutes as `1 h 05 min` / `45 min`; a dash when there is nothing to average. */
export function formatMinutes(minutes: number | null): string {
  if (minutes === null) return '–';
  const h = Math.floor(minutes / 60);
  const m = minutes % 60;
  return h ? `${h} h ${String(m).padStart(2, '0')} min` : `${m} min`;
}

/** Why the period cannot be run, or null (mirrors the API's rules). */
export function periodProblem(from: string, to: string): string | null {
  if (!from || !to) return 'Choose both dates.';
  if (from > to) return 'The start date must be on or before the end date.';
  const days = (Date.parse(to) - Date.parse(from)) / 86_400_000 + 1;
  return days > 366 ? 'A report covers at most 366 days.' : null;
}

@Injectable({ providedIn: 'root' })
export class ReportsApi {
  private readonly http = inject(HttpClient);

  technicians(f: ReportFilter): Observable<TechnicianReport> {
    return this.http.get<TechnicianReport>('/api/reports/technicians', { params: this.params('technicians', f) });
  }

  revenue(f: ReportFilter): Observable<RevenueReport> {
    return this.http.get<RevenueReport>('/api/reports/revenue', { params: this.params('revenue', f) });
  }

  partsUsage(f: ReportFilter): Observable<PartsUsageReport> {
    return this.http.get<PartsUsageReport>('/api/reports/parts-usage', { params: this.params('parts-usage', f) });
  }

  csv(kind: ReportKind, f: ReportFilter): Observable<{ blob: Blob; fileName: string }> {
    return this.http
      .get(`/api/reports/${kind}`, { params: this.params(kind, f).set('format', 'csv'), responseType: 'blob', observe: 'response' })
      .pipe(map((res) => ({ blob: res.body!, fileName: fileNameFrom(res.headers.get('Content-Disposition'), `${kind}.csv`) })));
  }

  private params(kind: ReportKind, f: ReportFilter): HttpParams {
    let params = new HttpParams().set('from', f.from).set('to', f.to);
    if (kind === 'revenue') params = params.set('groupBy', f.groupBy);
    return params;
  }
}
