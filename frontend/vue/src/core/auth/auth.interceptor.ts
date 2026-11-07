import { HttpError, type HttpInterceptor } from '../api/http';

export interface AuthInterceptorOptions {
  /** The session has expired. */
  sessionEnded: (returnUrl: string) => void;
  /** The page the user is on, to come back to after signing in again. */
  currentUrl: () => string;
}

/**
 * For API requests: sends the CSRF header the backend requires on state-changing requests, and
 * goes to the sign-in page when the session has expired.
 */
export function createAuthInterceptor({
  sessionEnded,
  currentUrl,
}: AuthInterceptorOptions): HttpInterceptor {
  return async (request, next) => {
    if (!request.url.startsWith('/api/')) {
      return next(request);
    }

    const headers = new Headers(request.headers);
    headers.set('X-CSRF', '1');
    try {
      return await next({ ...request, headers });
    } catch (error) {
      if (
        error instanceof HttpError &&
        error.status === 401 &&
        !request.url.startsWith('/api/auth/')
      ) {
        sessionEnded(currentUrl());
      }
      throw error;
    }
  };
}
