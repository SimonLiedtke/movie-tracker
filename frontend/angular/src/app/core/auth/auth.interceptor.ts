import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';
import { Auth } from './auth';

/**
 * For API requests: sends the CSRF header the backend requires on state-changing requests, and
 * goes to the sign-in page when the session has expired.
 */
export const authInterceptor: HttpInterceptorFn = (request, next) => {
  if (!request.url.startsWith('/api/')) {
    return next(request);
  }

  const auth = inject(Auth);
  const router = inject(Router);
  return next(request.clone({ setHeaders: { 'X-CSRF': '1' } })).pipe(
    catchError((error: unknown) => {
      if (
        error instanceof HttpErrorResponse &&
        error.status === 401 &&
        !request.url.startsWith('/api/auth/')
      ) {
        auth.sessionEnded(router.url);
      }
      return throwError(() => error);
    }),
  );
};
