import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

export const ASSET_TYPES = ['AC', 'Chiller', 'Generator', 'Elevator', 'Pump', 'Boiler', 'Electrical', 'Plumbing', 'Other'] as const;
export type AssetType = (typeof ASSET_TYPES)[number];
export type AssetStatus = 'Active' | 'OutOfService' | 'Retired';

export interface AssetInput {
  assetType: AssetType;
  name: string;
  manufacturer: string | null;
  model: string | null;
  serialNumber: string | null;
  installDate: string | null;
  warrantyExpiresOn: string | null;
  status: AssetStatus;
  notes: string | null;
}

export interface Asset extends AssetInput {
  id: string;
  siteId: string;
  siteName: string;
  underWarranty: boolean;
  isActive: boolean;
}

export interface AssetHistoryItem {
  workOrderId: string;
  workOrderNumber: string;
  date: string;
  type: string;
  technicianName: string | null;
  status: string;
  completionNotes: string | null;
}

@Injectable({ providedIn: 'root' })
export class AssetsApi {
  private readonly http = inject(HttpClient);

  forCustomer(customerId: string): Observable<Asset[]> {
    return this.http.get<Asset[]>(`/api/customers/${customerId}/assets`);
  }

  register(siteId: string, input: AssetInput): Observable<Asset> {
    return this.http.post<Asset>(`/api/sites/${siteId}/assets`, input);
  }

  update(id: string, input: AssetInput): Observable<Asset> {
    return this.http.put<Asset>(`/api/assets/${id}`, input);
  }

  history(id: string): Observable<AssetHistoryItem[]> {
    return this.http.get<AssetHistoryItem[]>(`/api/assets/${id}/history`);
  }
}
