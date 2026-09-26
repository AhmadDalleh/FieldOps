import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { AuthResponse } from '../shared/models/auth';
import { authInterceptor } from './auth.interceptor';
import { AuthService } from './auth.service';

function authResponse(accessToken: string, refreshToken: string): AuthResponse {
  return {
    accessToken,
    accessTokenExpiresAt: '2026-10-01T08:15:00Z',
    refreshToken,
    refreshTokenExpiresAt: '2026-10-08T08:00:00Z',
    user: { id: 'u1', email: 'd@fieldops.local', fullName: 'Dee', role: 'Dispatcher', technicianId: null },
  };
}

describe('authInterceptor', () => {
  let http: HttpClient;
  let backend: HttpTestingController;
  let auth: AuthService;

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({
      providers: [provideRouter([{ path: 'login', children: [] }]), provideHttpClient(withInterceptors([authInterceptor])), provideHttpClientTesting()],
    });
    http = TestBed.inject(HttpClient);
    backend = TestBed.inject(HttpTestingController);
    auth = TestBed.inject(AuthService);

    auth.login('d@fieldops.local', 'Pass123!').subscribe();
    backend.expectOne('/api/auth/login').flush(authResponse('access-1', 'refresh-1'));
  });

  afterEach(() => backend.verify());

  it('attaches the bearer token to API calls', () => {
    http.get('/api/users').subscribe();

    const req = backend.expectOne('/api/users');
    expect(req.request.headers.get('Authorization')).toBe('Bearer access-1');
    req.flush([]);
  });

  it('does not attach the token to the login call', () => {
    auth.login('d@fieldops.local', 'Pass123!').subscribe();

    const req = backend.expectOne('/api/auth/login');
    expect(req.request.headers.has('Authorization')).toBe(false);
    req.flush(authResponse('access-1', 'refresh-1'));
  });

  it('refreshes once on a 401 and retries with the new token', () => {
    let body: unknown;
    http.get('/api/users').subscribe((b) => (body = b));

    backend.expectOne('/api/users').flush(null, { status: 401, statusText: 'Unauthorized' });
    const refresh = backend.expectOne('/api/auth/refresh');
    expect(refresh.request.body).toEqual({ refreshToken: 'refresh-1' });
    refresh.flush(authResponse('access-2', 'refresh-2'));

    const retry = backend.expectOne('/api/users');
    expect(retry.request.headers.get('Authorization')).toBe('Bearer access-2');
    retry.flush({ ok: true });
    expect(body).toEqual({ ok: true });
  });

  it('shares one refresh between concurrent 401s', () => {
    http.get('/api/a').subscribe();
    http.get('/api/b').subscribe();

    backend.expectOne('/api/a').flush(null, { status: 401, statusText: 'Unauthorized' });
    backend.expectOne('/api/b').flush(null, { status: 401, statusText: 'Unauthorized' });
    backend.expectOne('/api/auth/refresh').flush(authResponse('access-2', 'refresh-2'));

    backend.expectOne('/api/a').flush({});
    backend.expectOne('/api/b').flush({});
  });

  it('clears the session when the refresh fails', () => {
    let failed = false;
    http.get('/api/users').subscribe({ error: () => (failed = true) });

    backend.expectOne('/api/users').flush(null, { status: 401, statusText: 'Unauthorized' });
    backend.expectOne('/api/auth/refresh').flush(null, { status: 401, statusText: 'Unauthorized' });

    expect(failed).toBe(true);
    expect(auth.isLoggedIn()).toBe(false);
  });

  it('passes other errors through without refreshing', () => {
    let status = 0;
    http.get('/api/users').subscribe({ error: (e) => (status = e.status) });

    backend.expectOne('/api/users').flush(null, { status: 403, statusText: 'Forbidden' });

    expect(status).toBe(403);
  });
});
