import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { Auth } from './auth';

/** For every page except the sign-in page: without a session, go to the sign-in page and come back afterwards. */
export const authGuard: CanActivateFn = (_route, state) =>
  inject(Auth).signedIn() ||
  inject(Router).createUrlTree(['/'], { queryParams: { returnUrl: state.url } });

/** For the sign-in page: signed-in users go straight to where they wanted to go. */
export const guestGuard: CanActivateFn = (route) => {
  if (!inject(Auth).signedIn()) {
    return true;
  }
  const returnUrl = route.queryParamMap.get('returnUrl');
  return inject(Router).parseUrl(returnUrl?.startsWith('/') ? returnUrl : '/movies');
};
