import { HttpErrorResponse, HttpInterceptorFn, HttpRequest } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, switchMap, throwError } from 'rxjs';
import { AuthService } from './auth.service';

const PUBLIC_AUTH_URLS = ['/api/auth/login', '/api/auth/refresh'];

function withToken(req: HttpRequest<unknown>, token: string | null): HttpRequest<unknown> {
  return token ? req.clone({ setHeaders: { Authorization: `Bearer ${token}` } }) : req;
}

/** Attaches the bearer token and, on a 401, refreshes once and retries the request. */
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  if (!req.url.startsWith('/api/') || PUBLIC_AUTH_URLS.includes(req.url)) return next(req);

  const auth = inject(AuthService);
  return next(withToken(req, auth.accessToken())).pipe(
    catchError((error: unknown) => {
      if (!(error instanceof HttpErrorResponse) || error.status !== 401 || !auth.isLoggedIn()) {
        return throwError(() => error);
      }
      return auth.refresh().pipe(
        catchError((refreshError: unknown) => {
          auth.clear();
          return throwError(() => refreshError);
        }),
        switchMap((token) => next(withToken(req, token))),
      );
    }),
  );
};
