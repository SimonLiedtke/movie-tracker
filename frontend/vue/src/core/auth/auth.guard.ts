import type { NavigationGuard } from 'vue-router';
import { isLocalPath, type Auth } from './auth';

/** For every page except the sign-in page: without a session, go to the sign-in page and come back afterwards. */
export function createAuthGuard(auth: Auth): NavigationGuard {
  return (to) => auth.signedIn.value || { path: '/', query: { returnUrl: to.fullPath } };
}

/** For the sign-in page: signed-in users go straight to where they wanted to go. */
export function createGuestGuard(auth: Auth): NavigationGuard {
  return (to) => {
    if (!auth.signedIn.value) {
      return true;
    }
    const returnUrl = [to.query['returnUrl']].flat()[0];
    return isLocalPath(returnUrl) ? returnUrl : '/movies';
  };
}
