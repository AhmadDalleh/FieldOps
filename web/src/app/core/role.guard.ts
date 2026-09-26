import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { Role } from '../shared/models/auth';
import { AuthService, homeUrlFor } from './auth.service';

/** Lets the route through only for the given roles; others go to login or to their own home. */
export function roleGuard(...roles: Role[]): CanActivateFn {
  return () => {
    const auth = inject(AuthService);
    const router = inject(Router);
    const user = auth.user();
    if (!user) return router.parseUrl('/login');
    return roles.includes(user.role) ? true : router.parseUrl(homeUrlFor(user.role));
  };
}

/** Sends a logged-in user from the root or login page to their home. */
export const homeRedirectGuard: CanActivateFn = () => {
  const user = inject(AuthService).user();
  return user ? inject(Router).parseUrl(homeUrlFor(user.role)) : true;
};
