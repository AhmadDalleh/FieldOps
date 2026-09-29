import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

export interface Skill {
  id: string;
  name: string;
}

export interface Technician {
  id: string;
  userId: string;
  fullName: string;
  email: string;
  employeeCode: string;
  phone: string | null;
  color: string;
  hourlyCost: number;
  /** `HH:mm:ss` */
  workingHoursStart: string;
  workingHoursEnd: string;
  isActive: boolean;
  skills: Skill[];
}

export interface TechnicianInput {
  employeeCode: string;
  phone: string | null;
  color: string;
  hourlyCost: number;
  workingHoursStart: string;
  workingHoursEnd: string;
  skillIds: string[];
}

export interface TimeOffSlot {
  id: string;
  startsAt: string;
  endsAt: string;
  reason: string | null;
}

export interface TechnicianAvailability {
  technician: Technician;
  date: string;
  jobCount: number;
  timeOff: TimeOffSlot[];
  isAvailable: boolean;
}

@Injectable({ providedIn: 'root' })
export class TechniciansApi {
  private readonly http = inject(HttpClient);

  list(date: string, includeInactive = false): Observable<TechnicianAvailability[]> {
    const params = new HttpParams().set('date', date).set('includeInactive', includeInactive);
    return this.http.get<TechnicianAvailability[]>('/api/technicians', { params });
  }

  get(id: string): Observable<Technician> {
    return this.http.get<Technician>(`/api/technicians/${id}`);
  }

  update(id: string, input: TechnicianInput): Observable<Technician> {
    return this.http.put<Technician>(`/api/technicians/${id}`, input);
  }

  skills(): Observable<Skill[]> {
    return this.http.get<Skill[]>('/api/skills');
  }

  createSkill(name: string): Observable<Skill> {
    return this.http.post<Skill>('/api/skills', { name });
  }

  renameSkill(id: string, name: string): Observable<Skill> {
    return this.http.put<Skill>(`/api/skills/${id}`, { name });
  }
}
