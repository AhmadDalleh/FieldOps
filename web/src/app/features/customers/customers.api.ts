import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { PagedResult } from '../../shared/models/paged-result';

export type CustomerType = 'Individual' | 'Business';

export interface CustomerInput {
  name: string;
  type: CustomerType;
  email: string | null;
  phone: string;
  taxRegistrationNumber: string | null;
  billingAddress: string | null;
  notes: string | null;
}

export interface Customer extends CustomerInput {
  id: string;
  code: string;
  isActive: boolean;
  createdAt: string;
}

export interface CustomerListItem {
  id: string;
  code: string;
  name: string;
  type: CustomerType;
  phone: string;
  email: string | null;
  isActive: boolean;
}

export interface ContactInput {
  name: string;
  email: string | null;
  phone: string | null;
  jobTitle: string | null;
  isPrimary: boolean;
}

export interface Contact extends ContactInput {
  id: string;
}

export interface SiteInput {
  name: string;
  addressLine1: string;
  addressLine2: string | null;
  city: string;
  region: string | null;
  country: string | null;
  latitude: number | null;
  longitude: number | null;
  accessNotes: string | null;
}

export interface Site extends SiteInput {
  id: string;
  customerId: string;
  country: string;
  isActive: boolean;
}

export interface CustomerQuery {
  page: number;
  pageSize: number;
  search: string;
  includeInactive: boolean;
}

@Injectable({ providedIn: 'root' })
export class CustomersApi {
  private readonly http = inject(HttpClient);

  list(q: CustomerQuery): Observable<PagedResult<CustomerListItem>> {
    const params = new HttpParams()
      .set('page', q.page)
      .set('pageSize', q.pageSize)
      .set('search', q.search)
      .set('includeInactive', q.includeInactive);
    return this.http.get<PagedResult<CustomerListItem>>('/api/customers', { params });
  }

  get(id: string): Observable<Customer> {
    return this.http.get<Customer>(`/api/customers/${id}`);
  }

  create(input: CustomerInput, force = false): Observable<Customer> {
    return this.http.post<Customer>('/api/customers', input, { params: force ? { force: true } : {} });
  }

  update(id: string, input: CustomerInput): Observable<Customer> {
    return this.http.put<Customer>(`/api/customers/${id}`, input);
  }

  deactivate(id: string): Observable<void> {
    return this.http.post<void>(`/api/customers/${id}/deactivate`, null);
  }

  contacts(customerId: string): Observable<Contact[]> {
    return this.http.get<Contact[]>(`/api/customers/${customerId}/contacts`);
  }

  addContact(customerId: string, input: ContactInput): Observable<Contact> {
    return this.http.post<Contact>(`/api/customers/${customerId}/contacts`, input);
  }

  updateContact(customerId: string, contactId: string, input: ContactInput): Observable<Contact> {
    return this.http.put<Contact>(`/api/customers/${customerId}/contacts/${contactId}`, input);
  }

  deleteContact(customerId: string, contactId: string): Observable<void> {
    return this.http.delete<void>(`/api/customers/${customerId}/contacts/${contactId}`);
  }

  sites(customerId: string, includeInactive: boolean): Observable<Site[]> {
    return this.http.get<Site[]>(`/api/customers/${customerId}/sites`, { params: { includeInactive } });
  }

  addSite(customerId: string, input: SiteInput): Observable<Site> {
    return this.http.post<Site>(`/api/customers/${customerId}/sites`, input);
  }

  updateSite(siteId: string, input: SiteInput): Observable<Site> {
    return this.http.put<Site>(`/api/sites/${siteId}`, input);
  }

  deactivateSite(siteId: string): Observable<void> {
    return this.http.post<void>(`/api/sites/${siteId}/deactivate`, null);
  }
}
