import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { ActivatedRouteSnapshot, Router, RouterStateSnapshot, UrlTree, provideRouter } from '@angular/router';
import { Role } from '../shared/models/auth';
import { AuthService, homeUrlFor } from './auth.service';
import { homeRedirectGuard, roleGuard } from './role.guard';

describe('role guards', () => {
  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({ providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()] });
  });

  function signInAs(role: Role): void {
    TestBed.inject(AuthService).login('x@fieldops.local', 'Pass123!').subscribe();
    TestBed.inject(HttpTestingController)
      .expectOne('/api/auth/login')
      .flush({
        accessToken: 'a',
        accessTokenExpiresAt: '',
        refreshToken: 'r',
        refreshTokenExpiresAt: '',
        user: { id: '1', email: 'x@fieldops.local', fullName: 'X', role, technicianId: null },
      });
  }

  function run(guard: ReturnType<typeof roleGuard>): boolean | string {
    const result = TestBed.runInInjectionContext(() => guard({} as ActivatedRouteSnapshot, {} as RouterStateSnapshot));
    return result instanceof UrlTree ? TestBed.inject(Router).serializeUrl(result) : (result as boolean);
  }

  it('sends anonymous users to login', () => {
    expect(run(roleGuard('Admin'))).toBe('/login');
  });

  it('lets allowed roles through', () => {
    signInAs('Dispatcher');
    expect(run(roleGuard('Admin', 'Dispatcher'))).toBe(true);
  });

  it('sends a technician away from office routes to their jobs', () => {
    signInAs('Technician');
    expect(run(roleGuard('Admin', 'Dispatcher'))).toBe('/tech/my-jobs');
  });

  it('sends a dispatcher away from admin-only routes to the dashboard', () => {
    signInAs('Dispatcher');
    expect(run(roleGuard('Admin'))).toBe('/office/dashboard');
  });

  it('redirects a logged-in user from login to their home', () => {
    signInAs('Admin');
    expect(run(homeRedirectGuard)).toBe('/office/dashboard');
  });

  it('maps technicians to my jobs and office staff to the dashboard', () => {
    expect(homeUrlFor('Technician')).toBe('/tech/my-jobs');
    expect(homeUrlFor('Admin')).toBe('/office/dashboard');
    expect(homeUrlFor('Dispatcher')).toBe('/office/dashboard');
  });
});
