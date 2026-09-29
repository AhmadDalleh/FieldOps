import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { fileNameFrom } from '../../shared/files/download';
import { PagedResult } from '../../shared/models/paged-result';

export type InvoiceStatus = 'Draft' | 'Issued' | 'Paid' | 'Void';
export type InvoiceLineType = 'Labor' | 'Part' | 'Other';

export const INVOICE_STATUSES: InvoiceStatus[] = ['Draft', 'Issued', 'Paid', 'Void'];
export const LINE_TYPES: InvoiceLineType[] = ['Labor', 'Part', 'Other'];

export interface InvoiceLine {
  id: string;
  lineType: InvoiceLineType;
  description: string;
  quantity: number;
  unitPrice: number;
  lineTotal: number;
}

export interface InvoiceLineInput {
  lineType: InvoiceLineType;
  description: string;
  quantity: number;
  unitPrice: number;
}

export interface Invoice {
  id: string;
  number: string | null;
  status: InvoiceStatus;
  workOrderId: string;
  workOrderNumber: string;
  workOrderTitle: string;
  customerId: string;
  customerName: string;
  issueDate: string | null;
  dueDate: string | null;
  isOverdue: boolean;
  subtotal: number;
  vatRate: number;
  vatAmount: number;
  total: number;
  currency: string;
  paidAt: string | null;
  paymentReference: string | null;
  voidReason: string | null;
  createdAt: string;
  lines: InvoiceLine[];
  version: number;
}

export interface InvoiceListItem {
  id: string;
  number: string | null;
  status: InvoiceStatus;
  workOrderId: string;
  workOrderNumber: string;
  customerId: string;
  customerName: string;
  issueDate: string | null;
  dueDate: string | null;
  total: number;
  isOverdue: boolean;
  createdAt: string;
}

export interface InvoiceQuery {
  page: number;
  pageSize: number;
  search: string;
  status: InvoiceStatus | null;
  customerId: string | null;
  from: string | null;
  to: string | null;
  overdue: boolean;
}

export interface InvoicePdf {
  blob: Blob;
  fileName: string;
}

/** The invoice's display name: its number once issued. */
export function invoiceName(i: { number: string | null; status: InvoiceStatus }): string {
  return i.number ?? 'Draft invoice';
}

/** Money rounded half away from zero to fils, like the server. */
export function roundMoney(x: number): number {
  return (Math.sign(x) * Math.round(Math.abs(x) * 100 + 1e-7)) / 100;
}

/** What a line comes to: quantity × unit price, rounded to fils. */
export function lineAmount(quantity: number | null, unitPrice: number | null): number {
  return roundMoney((quantity ?? 0) * (unitPrice ?? 0));
}

@Injectable({ providedIn: 'root' })
export class InvoicesApi {
  private readonly http = inject(HttpClient);

  list(q: InvoiceQuery): Observable<PagedResult<InvoiceListItem>> {
    let params = new HttpParams().set('page', q.page).set('pageSize', q.pageSize);
    if (q.search) params = params.set('search', q.search);
    if (q.status) params = params.set('status', q.status);
    if (q.customerId) params = params.set('customerId', q.customerId);
    if (q.from) params = params.set('from', q.from);
    if (q.to) params = params.set('to', q.to);
    if (q.overdue) params = params.set('overdue', true);
    return this.http.get<PagedResult<InvoiceListItem>>('/api/invoices', { params });
  }

  get(id: string): Observable<Invoice> {
    return this.http.get<Invoice>(`/api/invoices/${id}`);
  }

  generate(workOrderId: string): Observable<Invoice> {
    return this.http.post<Invoice>(`/api/work-orders/${workOrderId}/invoice`, null);
  }

  addLine(id: string, input: InvoiceLineInput): Observable<Invoice> {
    return this.http.post<Invoice>(`/api/invoices/${id}/lines`, input);
  }

  updateLine(id: string, lineId: string, input: InvoiceLineInput): Observable<Invoice> {
    return this.http.put<Invoice>(`/api/invoices/${id}/lines/${lineId}`, input);
  }

  removeLine(id: string, lineId: string): Observable<Invoice> {
    return this.http.delete<Invoice>(`/api/invoices/${id}/lines/${lineId}`);
  }

  issue(id: string): Observable<Invoice> {
    return this.http.post<Invoice>(`/api/invoices/${id}/issue`, null);
  }

  markPaid(id: string, paidAt: string, reference: string): Observable<Invoice> {
    return this.http.post<Invoice>(`/api/invoices/${id}/mark-paid`, { paidAt, reference });
  }

  void(id: string, reason: string): Observable<Invoice> {
    return this.http.post<Invoice>(`/api/invoices/${id}/void`, { reason });
  }

  delete(id: string): Observable<void> {
    return this.http.delete<void>(`/api/invoices/${id}`);
  }

  pdf(id: string): Observable<InvoicePdf> {
    return this.http.get(`/api/invoices/${id}/pdf`, { responseType: 'blob', observe: 'response' }).pipe(
      map((res) => ({ blob: res.body!, fileName: fileNameFrom(res.headers.get('Content-Disposition'), 'invoice.pdf') })),
    );
  }
}

/** A calendar date from the API (yyyy-mm-dd) as "1 Oct 2026". */
export function formatDay(date: string | null): string {
  if (!date) return '';
  return new Date(`${date}T00:00:00Z`).toLocaleDateString('en-GB', { day: 'numeric', month: 'short', year: 'numeric', timeZone: 'UTC' });
}
