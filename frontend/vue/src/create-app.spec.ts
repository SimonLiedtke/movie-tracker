import { flushPromises } from '@vue/test-utils';
import { describe, expect, it } from 'vitest';
import { useFakeBackend } from './test/fake-backend';
import { ALICE, useTestApp } from './test/start-app';

describe('the app', () => {
  const backend = useFakeBackend();
  const start = useTestApp(backend);

  const movies = { items: [], page: 1, pageSize: 24, totalCount: 0 };
  const answerMovieList = async () => {
    backend.expectOne('/api/genres').flush([]);
    backend.expectOne('/api/movies?sort=title&page=1&pageSize=24').flush(movies);
    await flushPromises();
  };

  it('shows the sign-in page to somebody who is not signed in', async () => {
    const app = await start();

    expect(app.router.currentRoute.value.fullPath).toBe('/');
    expect(app.text()).toContain('Sign in with Google');
    expect(document.title).toBe('Sign in · Movie Tracker');
  });

  it('sends a signed-in user from the sign-in page to the movies', async () => {
    const app = await start({ user: ALICE });
    await answerMovieList();

    expect(app.router.currentRoute.value.fullPath).toBe('/movies');
    expect(app.text()).toContain('My movies');
    expect(document.title).toBe('My movies · Movie Tracker');
  });

  it('asks for the sign-in first and remembers where the user wanted to go', async () => {
    const app = await start({ url: '/movies?page=2' });

    expect(app.router.currentRoute.value.path).toBe('/');
    expect(app.router.currentRoute.value.query).toEqual({ returnUrl: '/movies?page=2' });
    expect(app.container.querySelector('a.button')?.getAttribute('href')).toBe(
      '/api/auth/login?returnUrl=%2Fmovies%3Fpage%3D2',
    );
  });

  it('explains a failed sign-in', async () => {
    const app = await start({ url: '/?loginError=1' });

    expect(app.text()).toContain('Signing in did not work');
  });

  it('sends everybody from a page that does not exist to the start', async () => {
    const signedOut = await start({ url: '/nowhere' });
    expect(signedOut.router.currentRoute.value.fullPath).toBe('/');

    const signedIn = await start({ user: ALICE, url: '/nowhere' });
    await answerMovieList();
    expect(signedIn.router.currentRoute.value.fullPath).toBe('/movies');
  });

  it('sends the CSRF header to the API', async () => {
    await start({ user: ALICE });

    expect(backend.expectOne('/api/genres').headers.get('X-CSRF')).toBe('1');
    const request = backend.expectOne('/api/movies?sort=title&page=1&pageSize=24');
    expect(request.headers.get('X-CSRF')).toBe('1');
    request.flush(movies);
    await flushPromises();
  });

  it('goes back to the sign-in page when the session has expired', async () => {
    const app = await start({ user: ALICE });
    backend.expectOne('/api/genres').flush([]);

    backend
      .expectOne('/api/movies?sort=title&page=1&pageSize=24')
      .flush(null, { status: 401, statusText: 'Unauthorized' });
    await flushPromises();

    expect(app.auth.signedIn.value).toBe(false);
    expect(app.router.currentRoute.value.path).toBe('/');
    expect(app.router.currentRoute.value.query).toEqual({ returnUrl: '/movies' });
    expect(app.text()).toContain('Sign in with Google');
  });
});
