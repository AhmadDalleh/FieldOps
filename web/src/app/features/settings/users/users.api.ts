import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { Role } from '../../../shared/models/auth';
import { PagedResult } from '../../../shared/models/paged-result';

export interface User {
  id: string;
  email: string;
  fullName: string;
  phoneNumber: string | null;
  role: Role;
  isActive: boolean;
}

export interface UserInput {
  fullName: string;
  email: string;
  phoneNumber: string | null;
  role: Role;
}

@Injectable({ providedIn: 'root' })
export class UsersApi {
  private readonly http = inject(HttpClient);

  list(page: number, pageSize: number, search: string): Observable<PagedResult<User>> {
    const params = new HttpParams().set('page', page).set('pageSize', pageSize).set('search', search);
    return this.http.get<PagedResult<User>>('/api/users', { params });
  }

  create(input: UserInput & { temporaryPassword: string }): Observable<User> {
    return this.http.post<User>('/api/users', input);
  }

  update(id: string, input: UserInput): Observable<User> {
    return this.http.put<User>(`/api/users/${id}`, input);
  }

  setActive(id: string, active: boolean): Observable<void> {
    return this.http.post<void>(`/api/users/${id}/${active ? 'activate' : 'deactivate'}`, null);
  }
}
