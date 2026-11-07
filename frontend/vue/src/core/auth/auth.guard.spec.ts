import { computed } from 'vue';
import { createMemoryHistory, createRouter } from 'vue-router';
import { describe, expect, it } from 'vitest';
import type { Auth } from './auth';
import { createAuthGuard, createGuestGuard } from './auth.guard';

function setup(signedIn: boolean) {
  const auth = { signedIn: computed(() => signedIn) } as Auth;
  const page = { render: () => null };
  return createRouter({
    history: createMemoryHistory(),
    routes: [
      { path: '/', component: page, beforeEnter: createGuestGuard(auth) },
      { path: '/movies', component: page, beforeEnter: createAuthGuard(auth) },
      { path: '/diary', component: page, beforeEnter: createAuthGuard(auth) },
      { path: '/watchlist', component: page, beforeEnter: createAuthGuard(auth) },
    ],
  });
}

async function go(signedIn: boolean, url: string) {
  const router = setup(signedIn);
  await router.push(url);
  return router.currentRoute.value;
}

describe('authGuard', () => {
  it('lets signed-in users through', async () => {
    expect((await go(true, '/diary')).fullPath).toBe('/diary');
  });

  it('sends everybody else to the sign-in page and remembers where they wanted to go', async () => {
    const route = await go(false, '/diary?page=2');

    expect(route.path).toBe('/');
    expect(route.query).toEqual({ returnUrl: '/diary?page=2' });
  });
});

describe('guestGuard', () => {
  it('shows the sign-in page to signed-out users', async () => {
    expect((await go(false, '/')).fullPath).toBe('/');
  });

  it('sends signed-in users to the movies or to the page they wanted to go to', async () => {
    expect((await go(true, '/')).fullPath).toBe('/movies');
    expect((await go(true, '/?returnUrl=%2Fwatchlist')).fullPath).toBe('/watchlist');
    expect((await go(true, '/?returnUrl=%2Fdiary%3Fpage%3D2')).fullPath).toBe('/diary?page=2');
  });

  it('only goes to local URLs', async () => {
    expect((await go(true, '/?returnUrl=https%3A%2F%2Fevil.example.com')).fullPath).toBe('/movies');
    expect((await go(true, '/?returnUrl=%2F%2Fevil.example.com')).fullPath).toBe('/movies');
  });
});
