import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { Observable, finalize, map, shareReplay, tap } from 'rxjs';
import { AuthResponse, AuthUser, Role } from '../shared/models/auth';

interface StoredSession {
  accessToken: string;
  refreshToken: string;
  user: AuthUser;
}

const STORAGE_KEY = 'fieldops.session';

export function homeUrlFor(role: Role): string {
  return role === 'Technician' ? '/tech/my-jobs' : '/office/dashboard';
}

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);
  private readonly session = signal<StoredSession | null>(readSession());
  private refreshInFlight: Observable<string> | null = null;

  readonly user = computed(() => this.session()?.user ?? null);
  readonly isLoggedIn = computed(() => this.session() !== null);
  readonly accessToken = computed(() => this.session()?.accessToken ?? null);

  login(email: string, password: string): Observable<AuthUser> {
    return this.http.post<AuthResponse>('/api/auth/login', { email, password }).pipe(
      tap((res) => this.store(res)),
      map((res) => res.user),
    );
  }

  /** Exchanges the refresh token for a new pair. Concurrent callers share one request. */
  refresh(): Observable<string> {
    const refreshToken = this.session()?.refreshToken;
    if (!refreshToken) throw new Error('There is no session to refresh.');

    this.refreshInFlight ??= this.http.post<AuthResponse>('/api/auth/refresh', { refreshToken }).pipe(
      tap((res) => this.store(res)),
      map((res) => res.accessToken),
      finalize(() => (this.refreshInFlight = null)),
      shareReplay(1),
    );
    return this.refreshInFlight;
  }

  logout(): void {
    const refreshToken = this.session()?.refreshToken;
    if (refreshToken) {
      this.http.post('/api/auth/logout', { refreshToken }).subscribe({ error: () => undefined });
    }
    this.clear();
  }

  /** Drops the local session without calling the API, e.g. after a failed refresh. */
  clear(): void {
    this.session.set(null);
    try {
      localStorage.removeItem(STORAGE_KEY);
    } catch {
      // Storage can be unavailable (private mode); the in-memory session is already cleared.
    }
    void this.router.navigateByUrl('/login');
  }

  private store(res: AuthResponse): void {
    const session: StoredSession = { accessToken: res.accessToken, refreshToken: res.refreshToken, user: res.user };
    this.session.set(session);
    try {
      localStorage.setItem(STORAGE_KEY, JSON.stringify(session));
    } catch {
      // Without storage the session simply does not survive a reload.
    }
  }
}

function readSession(): StoredSession | null {
  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    return raw ? (JSON.parse(raw) as StoredSession) : null;
  } catch {
    return null;
  }
}
