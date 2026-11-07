import { isAxiosError } from 'axios';
import type { Http } from '../api/http';

export interface AuthInterceptorOptions {
  /** The session has expired. */
  sessionEnded: (returnUrl: string) => void;
  /** The page the user is on, to come back to after signing in again. */
  currentUrl: () => string;
}

const isApi = (url: string | undefined) => !!url?.startsWith('/api/');

/**
 * For API requests: sends the CSRF header the backend requires on state-changing requests, and
 * goes to the sign-in page when the session has expired.
 */
export function addAuthInterceptors(
  http: Http,
  { sessionEnded, currentUrl }: AuthInterceptorOptions,
): void {
  // Synchronous: it does not wait for anything, so axios need not wait a tick before it sends the request.
  http.interceptors.request.use(
    (config) => {
      if (isApi(config.url)) {
        config.headers.set('X-CSRF', '1');
      }
      return config;
    },
    null,
    { synchronous: true },
  );

  http.interceptors.response.use(undefined, (error: unknown) => {
    // The auth endpoints answer 401 for "not signed in": that is for the Auth service to handle.
    if (
      isAxiosError(error) &&
      error.response?.status === 401 &&
      isApi(error.config?.url) &&
      !error.config?.url?.startsWith('/api/auth/')
    ) {
      sessionEnded(currentUrl());
    }
    return Promise.reject(error);
  });
}
