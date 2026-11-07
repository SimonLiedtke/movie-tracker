import { flushPromises, mount } from '@vue/test-utils';
import { computed } from 'vue';
import { describe, expect, it, vi } from 'vitest';
import type { User } from './core/api/models';
import type { Auth } from './core/auth/auth';
import App from './App.vue';
import { createTestRouter, provideServices } from './test/services';

const ALICE: User = { id: '1', email: 'alice@example.com', name: 'Alice', pictureUrl: null };

async function render(user: User | null, url = '/movies') {
  const logout = vi.fn(() => Promise.resolve());
  const router = createTestRouter();
  await router.push(url);
  const app = mount(App, {
    global: {
      plugins: [
        router,
        provideServices({ auth: { user: computed(() => user), logout } as Partial<Auth> }),
      ],
    },
  });
  return { app, router, logout };
}

describe('App', () => {
  it('renders the navigation and the user for signed-in users', async () => {
    const { app } = await render(ALICE);

    const links = app.findAll('.navbar-start a');
    expect(links.map((link) => link.text())).toEqual(['My movies', 'Diary', 'Watchlist', 'Import']);
    expect(links.map((link) => link.attributes('href'))).toEqual([
      '/movies',
      '/diary',
      '/watchlist',
      '/import',
    ]);
    expect(app.get('.navbar-end').text()).toContain('Alice');
    expect(app.get('.navbar-end span[title]').attributes('title')).toBe('alice@example.com');
    expect(app.get('.navbar-end button').text()).toContain('Sign out');
    expect(app.get('.navbar-end a.button').attributes('href')).toBe('/diary/new');
    expect(app.find('.navbar-end img').exists()).toBe(false);
  });

  it('shows the picture of the user', async () => {
    const { app } = await render({ ...ALICE, pictureUrl: 'https://image/alice.jpg' });

    const picture = app.get('.navbar-end img');
    expect(picture.attributes('src')).toBe('https://image/alice.jpg');
    expect(picture.attributes('referrerpolicy')).toBe('no-referrer');
  });

  it('hides the navigation on the sign-in page', async () => {
    const { app } = await render(null, '/');

    expect(app.find('nav.navbar').exists()).toBe(false);
  });

  it('marks the section of the current page, also for the pages below it', async () => {
    const { app, router } = await render(ALICE, '/movies/42');
    const selected = () => app.findAll('.navbar-start a.is-selected').map((link) => link.text());

    expect(selected()).toEqual(['My movies']);

    await router.push('/diary/new');
    expect(selected()).toEqual(['Diary']);

    await router.push('/watchlist?page=2');
    expect(selected()).toEqual(['Watchlist']);

    await router.push('/moviesx'); // Not below /movies.
    expect(selected()).toEqual([]);
  });

  it('opens and closes the menu on small screens', async () => {
    const { app } = await render(ALICE);
    const menu = () => app.get('.navbar-menu');
    const burger = () => app.get('.navbar-burger');
    expect(menu().classes()).not.toContain('is-active');
    expect(burger().attributes('aria-expanded')).toBe('false');

    await burger().trigger('click');
    expect(menu().classes()).toContain('is-active');
    expect(burger().classes()).toContain('is-active');
    expect(burger().attributes('aria-expanded')).toBe('true');

    await burger().trigger('click');
    expect(menu().classes()).not.toContain('is-active');
  });

  it('closes the menu when the user goes somewhere', async () => {
    for (const link of ['.navbar-brand a.navbar-item', '.navbar-start a', '.navbar-end a.button']) {
      const { app } = await render(ALICE);
      await app.get('.navbar-burger').trigger('click');

      await app.get(link).trigger('click');
      await flushPromises();

      expect(app.get('.navbar-menu').classes()).not.toContain('is-active');
    }
  });

  it('signs out', async () => {
    const { app, logout } = await render(ALICE);
    await app.get('.navbar-burger').trigger('click');

    await app.get('.navbar-end button').trigger('click');

    expect(logout).toHaveBeenCalledOnce();
    expect(app.get('.navbar-menu').classes()).not.toContain('is-active');
  });

  it('names TMDB as the source of the movie data', async () => {
    const { app } = await render(ALICE);

    const link = app.get('footer a');
    expect(link.text()).toBe('TMDB');
    expect(link.attributes('href')).toBe('https://www.themoviedb.org/');
    expect(link.attributes('target')).toBe('_blank');
    expect(app.get('footer').text()).toContain('not endorsed or certified by TMDB');
  });
});
