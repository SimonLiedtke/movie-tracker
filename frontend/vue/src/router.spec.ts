import { computed } from 'vue';
import { createMemoryHistory } from 'vue-router';
import { describe, expect, it } from 'vitest';
import type { Auth } from './core/auth/auth';
import { createAppRouter } from './router';

function setup(signedIn: boolean) {
  const auth = { signedIn: computed(() => signedIn) } as Auth;
  return createAppRouter(auth, createMemoryHistory());
}

async function go(signedIn: boolean, url: string) {
  const router = setup(signedIn);
  await router.push(url);
  return router.currentRoute.value;
}

// Every page of the app, with the title of the browser tab.
const PAGES: [url: string, title: string][] = [
  ['/movies', 'My movies · Movie Tracker'],
  ['/movies/new', 'Add a movie manually · Movie Tracker'],
  ['/movies/42', 'Movie · Movie Tracker'],
  ['/movies/42/edit', 'Edit movie · Movie Tracker'],
  ['/diary', 'Diary · Movie Tracker'],
  ['/diary/new', 'Log a viewing · Movie Tracker'],
  ['/diary/42/edit', 'Edit diary entry · Movie Tracker'],
  ['/watchlist', 'Watchlist · Movie Tracker'],
  ['/activity', 'Activity log · Movie Tracker'],
  ['/import', 'Import from Letterboxd · Movie Tracker'],
];

describe('router', () => {
  it.each(PAGES)('shows %s to a signed-in user, with the title "%s"', async (url, title) => {
    const route = await go(true, url);

    expect(route.fullPath).toBe(url);
    expect(document.title).toBe(title);
  });

  it.each(PAGES)('asks somebody who is not signed in for the sign-in first: %s', async (url) => {
    const route = await go(false, url);

    expect(route.path).toBe('/');
    expect(route.query).toEqual({ returnUrl: url });
    expect(document.title).toBe('Sign in · Movie Tracker');
  });

  it('remembers the query of the page the user wanted to see', async () => {
    const route = await go(false, '/diary?from=2026-10-01&page=2');

    expect(route.query).toEqual({ returnUrl: '/diary?from=2026-10-01&page=2' });
  });

  it('does not mix up the pages of a movie', async () => {
    const router = setup(true);

    await router.push('/movies/new');
    expect(router.currentRoute.value.name).toBe('movie-new');
    await router.push('/movies/new-year');
    expect(router.currentRoute.value.name).toBe('movie');
    expect(router.currentRoute.value.params).toEqual({ id: 'new-year' });
    await router.push('/movies/7/edit');
    expect(router.currentRoute.value.name).toBe('movie-edit');
    expect(router.currentRoute.value.params).toEqual({ id: '7' });
  });

  it('leads every other URL to the start, and keeps its query', async () => {
    const signedOut = await go(false, '/nowhere/at/all?x=1');
    expect(signedOut.path).toBe('/');
    expect(signedOut.query).toEqual({ x: '1' });

    const signedIn = await go(true, '/nowhere');
    expect(signedIn.fullPath).toBe('/movies');
  });

  it('shows the sign-in page to somebody who is not signed in, and nobody else', async () => {
    expect((await go(false, '/')).fullPath).toBe('/');
    expect((await go(true, '/')).fullPath).toBe('/movies');
    expect((await go(true, '/?returnUrl=%2Fwatchlist')).fullPath).toBe('/watchlist');
  });
});
