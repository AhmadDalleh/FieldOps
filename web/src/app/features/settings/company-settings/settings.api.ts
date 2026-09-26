import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

export interface CompanySettings {
  companyName: string;
  companyAddress: string | null;
  trn: string | null;
  hasLogo: boolean;
  vatRate: number;
  laborRatePerHour: number;
  invoiceDueDays: number;
  currency: string;
}

export type CompanySettingsInput = Omit<CompanySettings, 'hasLogo'>;

@Injectable({ providedIn: 'root' })
export class SettingsApi {
  private readonly http = inject(HttpClient);

  get(): Observable<CompanySettings> {
    return this.http.get<CompanySettings>('/api/settings');
  }

  update(input: CompanySettingsInput): Observable<CompanySettings> {
    return this.http.put<CompanySettings>('/api/settings', input);
  }

  uploadLogo(file: File): Observable<void> {
    const body = new FormData();
    body.append('file', file);
    return this.http.post<void>('/api/settings/logo', body);
  }
}
