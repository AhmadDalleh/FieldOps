import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { PagedResult } from '../../shared/models/paged-result';

export type PartUnit = 'Pcs' | 'M' | 'Kg' | 'L';
export type StockLocationType = 'Warehouse' | 'Van';
export type StockMovementType = 'Receive' | 'Transfer' | 'Consume' | 'Return' | 'Adjust';

export const UNITS: { value: PartUnit; label: string }[] = [
  { value: 'Pcs', label: 'pcs' },
  { value: 'M', label: 'm' },
  { value: 'Kg', label: 'kg' },
  { value: 'L', label: 'l' },
];
export const MOVEMENT_TYPES: StockMovementType[] = ['Receive', 'Transfer', 'Consume', 'Return', 'Adjust'];

export function unitLabel(unit: PartUnit): string {
  return UNITS.find((u) => u.value === unit)?.label ?? unit;
}

export interface Part {
  id: string;
  sku: string;
  name: string;
  description: string | null;
  unit: PartUnit;
  unitCost: number;
  unitPrice: number;
  reorderLevel: number;
  isActive: boolean;
  totalQuantity: number;
  isLow: boolean;
}

export interface PartInput {
  sku: string;
  name: string;
  description: string | null;
  unit: PartUnit;
  unitCost: number;
  unitPrice: number;
  reorderLevel: number;
  isActive: boolean;
}

export interface PartQuery {
  page: number;
  pageSize: number;
  search: string;
  includeInactive: boolean;
  lowOnly: boolean;
}

export interface StockLocation {
  id: string;
  name: string;
  type: StockLocationType;
  technicianId: string | null;
  isActive: boolean;
}

export interface StockRow {
  partId: string;
  sku: string;
  name: string;
  unit: PartUnit;
  reorderLevel: number;
  total: number;
  isLow: boolean;
  levels: { locationId: string; quantity: number }[];
}

export interface StockMovement {
  id: string;
  createdAt: string;
  type: StockMovementType;
  partId: string;
  sku: string;
  partName: string;
  quantity: number;
  fromLocation: string | null;
  toLocation: string | null;
  workOrderId: string | null;
  workOrderNumber: string | null;
  reason: string | null;
  createdByName: string;
}

export interface MovementQuery {
  page: number;
  pageSize: number;
  partId: string | null;
  type: StockMovementType | null;
  locationId: string | null;
  from: string | null;
  to: string | null;
}

@Injectable({ providedIn: 'root' })
export class InventoryApi {
  private readonly http = inject(HttpClient);

  parts(q: PartQuery): Observable<PagedResult<Part>> {
    let params = new HttpParams().set('page', q.page).set('pageSize', q.pageSize)
      .set('includeInactive', q.includeInactive).set('lowOnly', q.lowOnly);
    if (q.search) params = params.set('search', q.search);
    return this.http.get<PagedResult<Part>>('/api/parts', { params });
  }

  createPart(input: PartInput): Observable<Part> {
    return this.http.post<Part>('/api/parts', input);
  }

  updatePart(id: string, input: PartInput): Observable<Part> {
    return this.http.put<Part>(`/api/parts/${id}`, input);
  }

  locations(): Observable<StockLocation[]> {
    return this.http.get<StockLocation[]>('/api/stock-locations');
  }

  stock(lowOnly = false): Observable<StockRow[]> {
    return this.http.get<StockRow[]>('/api/stock', { params: new HttpParams().set('lowOnly', lowOnly) });
  }

  receive(partId: string, locationId: string, quantity: number): Observable<StockRow> {
    return this.http.post<StockRow>('/api/stock/receive', { partId, locationId, quantity });
  }

  transfer(partId: string, fromLocationId: string, toLocationId: string, quantity: number): Observable<StockRow> {
    return this.http.post<StockRow>('/api/stock/transfer', { partId, fromLocationId, toLocationId, quantity });
  }

  adjust(partId: string, locationId: string, newQuantity: number, reason: string): Observable<StockRow> {
    return this.http.post<StockRow>('/api/stock/adjust', { partId, locationId, newQuantity, reason });
  }

  movements(q: MovementQuery): Observable<PagedResult<StockMovement>> {
    let params = new HttpParams().set('page', q.page).set('pageSize', q.pageSize);
    if (q.partId) params = params.set('partId', q.partId);
    if (q.type) params = params.set('type', q.type);
    if (q.locationId) params = params.set('locationId', q.locationId);
    if (q.from) params = params.set('from', q.from);
    if (q.to) params = params.set('to', q.to);
    return this.http.get<PagedResult<StockMovement>>('/api/stock/movements', { params });
  }
}

/** Quantities have at most two decimals and must be above zero (the API's rule). */
export function isValidQuantity(value: number | null | undefined): boolean {
  return value != null && value > 0 && Math.abs(Math.round(value * 100) - value * 100) < 1e-6;
}

export interface WorkOrderPart {
  id: string;
  partId: string;
  sku: string;
  name: string;
  unit: PartUnit;
  quantity: number;
  unitPrice: number;
  lineTotal: number;
  locationName: string;
  canRemove: boolean;
}

export interface VanStockItem {
  partId: string;
  sku: string;
  name: string;
  unit: PartUnit;
  quantity: number;
  unitPrice: number;
}

/** Parts used on a work order (US-INV-05) and the signed-in technician's van stock. */
@Injectable({ providedIn: 'root' })
export class WorkOrderPartsApi {
  private readonly http = inject(HttpClient);

  list(workOrderId: string): Observable<WorkOrderPart[]> {
    return this.http.get<WorkOrderPart[]>(`/api/work-orders/${workOrderId}/parts`);
  }

  /** Returns the job's lines after the change. */
  add(workOrderId: string, partId: string, quantity: number): Observable<WorkOrderPart[]> {
    return this.http.post<WorkOrderPart[]>(`/api/work-orders/${workOrderId}/parts`, { partId, quantity });
  }

  /** Returns the job's lines after the parts went back to where they came from. */
  remove(workOrderId: string, lineId: string): Observable<WorkOrderPart[]> {
    return this.http.delete<WorkOrderPart[]>(`/api/work-orders/${workOrderId}/parts/${lineId}`);
  }

  vanStock(): Observable<VanStockItem[]> {
    return this.http.get<VanStockItem[]>('/api/me/van-stock');
  }
}
