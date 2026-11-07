import { computed, inject, ref, type InjectionKey } from 'vue';
import type { RouteLocationRaw } from 'vue-router';
import { HttpError, type Http } from '../api/http';
import type { User } from '../api/models';

export interface AuthOptions {
  http: Http;
  /** Goes to a page. Passed in because the router, which needs the auth service for its guards, is created after it. */
  navigate: (to: RouteLocationRaw) => unknown;
}

/**
 * The signed-in user. Signing in happens on the backend (Google OAuth), which sets an HttpOnly
 * session cookie; the frontend never handles tokens, it only asks the API who is signed in.
 */
export function createAuth({ http, navigate }: AuthOptions) {
  const currentUser = ref<User | null>(null);

  /** Asks the API for the signed-in user. Called once on app start. */
  async function load(): Promise<void> {
    try {
      currentUser.value = await http.get<User>('/api/auth/me');
    } catch (error) {
      if (!(error instanceof HttpError && error.status === 401)) {
        console.error('Could not load the signed-in user', error);
      }
      currentUser.value = null;
    }
  }

  /** URL that starts the Google sign-in; the browser navigates there (no XHR). */
  function loginUrl(returnUrl = '/movies'): string {
    return `/api/auth/login?returnUrl=${encodeURIComponent(returnUrl)}`;
  }

  async function logout(): Promise<void> {
    try {
      await http.post<void>('/api/auth/logout', null);
    } finally {
      sessionEnded();
    }
  }

  /** The session is gone (signed out or expired): back to the sign-in page. */
  function sessionEnded(returnUrl?: string): void {
    currentUser.value = null;
    navigate({ path: '/', query: returnUrl ? { returnUrl } : {} });
  }

  return {
    user: computed(() => currentUser.value),
    signedIn: computed(() => currentUser.value !== null),
    load,
    loginUrl,
    logout,
    sessionEnded,
  };
}

export type Auth = ReturnType<typeof createAuth>;

export const authKey: InjectionKey<Auth> = Symbol('auth');

/** The signed-in user, for components (provided by the app, see `createMovieTrackerApp`). */
export function useAuth(): Auth {
  const auth = inject(authKey);
  if (!auth) {
    throw new Error('The auth service is not provided.');
  }
  return auth;
}

/** A path within this app, e.g. "/diary?page=2": never another site, as in "//example.com". */
export function isLocalPath(url: string | null | undefined): url is string {
  return !!url && url.startsWith('/') && !url.startsWith('//') && !url.startsWith('/\\');
}
