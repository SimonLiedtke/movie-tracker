import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import {
  ActivatedRouteSnapshot,
  Router,
  RouterStateSnapshot,
  UrlTree,
  convertToParamMap,
  provideRouter,
} from '@angular/router';
import { Auth } from './auth';
import { authGuard, guestGuard } from './auth.guard';

function setup(signedIn: boolean) {
  TestBed.configureTestingModule({
    providers: [provideRouter([]), { provide: Auth, useValue: { signedIn: signal(signedIn) } }],
  });
  return TestBed.inject(Router);
}

function route(queryParams: Record<string, string> = {}): ActivatedRouteSnapshot {
  return { queryParamMap: convertToParamMap(queryParams) } as ActivatedRouteSnapshot;
}

const run = (guard: typeof authGuard, snapshot: ActivatedRouteSnapshot, url = '/') =>
  TestBed.runInInjectionContext(() => guard(snapshot, { url } as RouterStateSnapshot));

describe('authGuard', () => {
  it('lets signed-in users through', () => {
    setup(true);
    expect(run(authGuard, route(), '/diary')).toBe(true);
  });

  it('sends everybody else to the sign-in page and remembers where they wanted to go', () => {
    const router = setup(false);
    const result = run(authGuard, route(), '/diary?page=2') as UrlTree;
    expect(router.serializeUrl(result)).toBe('/?returnUrl=%2Fdiary%3Fpage%3D2');
  });
});

describe('guestGuard', () => {
  it('shows the sign-in page to signed-out users', () => {
    setup(false);
    expect(run(guestGuard, route())).toBe(true);
  });

  it('sends signed-in users to the movies or to the page they wanted to go to', () => {
    const router = setup(true);
    expect(router.serializeUrl(run(guestGuard, route()) as UrlTree)).toBe('/movies');
    expect(
      router.serializeUrl(run(guestGuard, route({ returnUrl: '/watchlist' })) as UrlTree),
    ).toBe('/watchlist');
    // Only local URLs.
    expect(
      router.serializeUrl(
        run(guestGuard, route({ returnUrl: 'https://evil.example.com' })) as UrlTree,
      ),
    ).toBe('/movies');
  });
});
