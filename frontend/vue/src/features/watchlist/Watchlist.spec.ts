import { flushPromises, type VueWrapper } from '@vue/test-utils';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { createApi } from '../../core/api/api';
import { createHttp } from '../../core/api/http';
import type { Movie, PagedResponse, WatchlistItem } from '../../core/api/models';
import { watchlistProps } from '../../route-props';
import { useFakeBackend } from '../../test/fake-backend';
import { mountRoute } from '../../test/services';
import Watchlist from './Watchlist.vue';

const item = (id: string, title: string, changes: Partial<WatchlistItem> = {}): WatchlistItem => ({
  movie: { id, title, releaseDate: '1995-12-15', posterUrl: null },
  addedAt: '2026-11-05T21:30:00Z',
  note: null,
  ...changes,
});

const HEAT = item('m1', 'Heat', {
  note: 'Recommended by Alice.',
  movie: {
    id: 'm1',
    title: 'Heat',
    releaseDate: '1995-12-15',
    posterUrl: 'https://image/heat.jpg',
  },
});
const ALIEN = item('m2', 'Alien', {
  movie: { id: 'm2', title: 'Alien', releaseDate: null, posterUrl: null },
});

const paged = (
  items: WatchlistItem[],
  { page = 1, totalCount = items.length } = {},
): PagedResponse<WatchlistItem> => ({ items, page, pageSize: 20, totalCount });

describe('Watchlist', () => {
  const backend = useFakeBackend();

  afterEach(() => vi.useRealTimers());

  const open = (url = '/watchlist') =>
    mountRoute(
      url,
      { path: '/watchlist', component: Watchlist, props: watchlistProps },
      { api: createApi(createHttp()) },
    );

  const listRequest = (page = 1) => backend.expectOne(`/api/watchlist?page=${page}&pageSize=20`);
  const text = (page: VueWrapper) => page.text().replace(/\s+/g, ' ');
  const buttonOf = (page: VueWrapper, label: string, index = 0) =>
    page.findAll('button, a.button').filter((b) => b.text() === label)[index];

  async function openWith(items: WatchlistItem[], url = '/watchlist', response = paged(items)) {
    const opened = await open(url);
    listRequest(response.page).flush(response);
    await flushPromises();
    return opened;
  }

  it('lists the movies to watch, with the note and when they were added', async () => {
    const { page } = await open();
    expect(page.find('progress').exists()).toBe(true);
    listRequest().flush(paged([HEAT, ALIEN]));
    await flushPromises();

    expect(page.find('progress').exists()).toBe(false);
    expect(text(page)).toContain('2 movies to watch');
    const items = page.findAll('article');
    expect(items).toHaveLength(2);
    expect(items[0].get('.media-content a').text()).toBe('Heat');
    expect(items[0].get('.media-content a').attributes('href')).toBe('/movies/m1');
    expect(items[0].get('.media-content p').text()).toBe('Heat (1995)');
    expect(items[0].get('img').attributes('src')).toBe('https://image/heat.jpg');
    expect(items[0].text()).toContain('Added 2026-11-05');
    expect(items[0].get('.pre-wrap').text()).toBe('Recommended by Alice.');
    expect(items[1].get('.media-content p').text()).toBe('Alien');
    expect(items[1].find('.pre-wrap').exists()).toBe(false);
  });

  it('says "1 movie" for one movie', async () => {
    const { page } = await openWith([HEAT]);

    expect(text(page)).toContain('1 movie to watch');
  });

  it('says when the watchlist is empty', async () => {
    const { page } = await openWith([]);

    expect(text(page)).toContain('Your watchlist is empty. Search a movie above to add it.');
  });

  it('offers to log a viewing, edit the note and remove for each movie', async () => {
    const { page } = await openWith([HEAT, ALIEN]);

    const buttons = (index: number) =>
      page
        .findAll('article')
        [index].findAll('.media-right .button')
        .map((b) => b.text());
    expect(buttons(0)).toEqual(['Log a viewing', 'Edit note', 'Remove']); // It has a note.
    expect(buttons(1)).toEqual(['Log a viewing', 'Add note', 'Remove']);
    expect(page.get('.media-right a.button').attributes('href')).toBe('/diary/new?movieId=m1');
  });

  it('shows the page of the URL', async () => {
    const { page } = await openWith(
      [HEAT],
      '/watchlist?page=2',
      paged([HEAT], { page: 2, totalCount: 21 }),
    );

    expect(page.get('.pagination-link.is-current').text()).toBe('2');
  });

  it('goes to another page', async () => {
    const { page, router } = await openWith(
      [HEAT],
      '/watchlist',
      paged([HEAT], { totalCount: 45 }),
    );

    await page.findAll('.pagination-link')[2].trigger('click');
    await flushPromises();

    expect(router.currentRoute.value.fullPath).toBe('/watchlist?page=3');
    listRequest(3).flush(paged([ALIEN], { page: 3, totalCount: 45 }));
    await flushPromises();
    expect(page.findAll('article')).toHaveLength(1);
  });

  it('shows why the watchlist cannot be loaded, until it is dismissed', async () => {
    const { page } = await open();
    listRequest().networkError();
    await flushPromises();

    expect(page.get('[role=alert]').text()).toContain('cannot be reached');
    await page.get('button.delete').trigger('click');
    expect(page.find('[role=alert]').exists()).toBe(false);
  });

  describe('adding a movie', () => {
    /** Searches for "heat" and picks the result that is one of the user's movies. */
    async function pickHeat(page: VueWrapper) {
      vi.useFakeTimers({ toFake: ['setTimeout', 'clearTimeout'] });
      await page.get('#add').setValue('heat');
      await vi.advanceTimersByTimeAsync(250);
      backend.expectOne('/api/movies/search?query=heat').flush({
        tmdbAvailable: true,
        items: [
          {
            movieId: 'm1',
            tmdbId: 949,
            title: 'Heat',
            originalTitle: 'Heat',
            releaseDate: '1995-12-15',
            posterUrl: null,
          },
        ],
      });
      await flushPromises();
      await page.get('a.dropdown-item').trigger('click');
      vi.useRealTimers();
    }

    const movie = (onWatchlist: boolean) => ({ id: 'm1', title: 'Heat', onWatchlist }) as Movie;

    it('puts the movie on the watchlist, and shows the list again', async () => {
      const { page } = await openWith([ALIEN]);

      await pickHeat(page);
      backend.expectOne('/api/movies/m1').flush(movie(false));
      await flushPromises();
      const put = backend.expectOne('/api/watchlist/m1');
      expect(put.method).toBe('PUT');
      expect(put.json).toEqual({ note: null });
      put.flush(HEAT);
      await flushPromises();

      listRequest().flush(paged([HEAT, ALIEN]));
      await flushPromises();
      expect(page.findAll('article')).toHaveLength(2);
    });

    it('leaves a movie that is on the watchlist already alone, so that its note stays', async () => {
      const { page } = await openWith([HEAT]);

      await pickHeat(page);
      backend.expectOne('/api/movies/m1').flush(movie(true));
      await flushPromises();

      // No PUT, which would replace the note. The list is shown again.
      listRequest().flush(paged([HEAT]));
      await flushPromises();
    });

    it('shows the first page, where the new movie is, if the user was on another one', async () => {
      const { page, router } = await openWith(
        [ALIEN],
        '/watchlist?page=2',
        paged([ALIEN], { page: 2, totalCount: 21 }),
      );

      await pickHeat(page);
      backend.expectOne('/api/movies/m1').flush(movie(false));
      await flushPromises();
      backend.expectOne('/api/watchlist/m1').flush(HEAT);
      await flushPromises();

      expect(router.currentRoute.value.fullPath).toBe('/watchlist?page=1');
      listRequest(1).flush(paged([HEAT], { totalCount: 21 }));
      await flushPromises();
    });

    it('shows why the movie could not be added', async () => {
      const { page } = await openWith([ALIEN]);

      await pickHeat(page);
      backend.expectOne('/api/movies/m1').flush(movie(false));
      await flushPromises();
      backend
        .expectOne('/api/watchlist/m1')
        .flush({ detail: 'Something broke.' }, { status: 500, statusText: 'Error' });
      await flushPromises();

      expect(page.get('[role=alert]').text()).toContain('Something broke.');
    });
  });

  describe('notes', () => {
    it('edits the note of a movie', async () => {
      const { page } = await openWith([HEAT, ALIEN]);

      await buttonOf(page, 'Edit note').trigger('click');
      const field = page.get<HTMLInputElement>('article input');
      expect(field.element.value).toBe('Recommended by Alice.');
      expect(field.attributes('maxlength')).toBe('500');
      expect(field.attributes('placeholder')).toBe('Why do you want to watch it?');
      expect(page.find('.pre-wrap').exists()).toBe(false); // The text is in the field now.

      await field.setValue('  Watch with Bob. ');
      await page.get('article form').trigger('submit');
      expect(page.get('article form button[type=submit]').classes()).toContain('is-loading');
      const request = backend.expectOne('/api/watchlist/m1');
      expect(request.method).toBe('PUT');
      expect(request.json).toEqual({ note: 'Watch with Bob.' });
      request.flush({ ...HEAT, note: 'Watch with Bob.' });
      await flushPromises();

      // The list is updated without loading it again.
      expect(page.find('form').exists()).toBe(false);
      expect(page.get('.pre-wrap').text()).toBe('Watch with Bob.');
      expect(page.findAll('article')).toHaveLength(2);
    });

    it('adds a note to a movie that has none, and removes it with an empty one', async () => {
      const { page } = await openWith([ALIEN]);

      await buttonOf(page, 'Add note').trigger('click');
      expect(page.get<HTMLInputElement>('article input').element.value).toBe('');
      await page.get('article input').setValue('Classic.');
      await page.get('article form').trigger('submit');
      backend.expectOne('/api/watchlist/m2').flush({ ...ALIEN, note: 'Classic.' });
      await flushPromises();
      expect(page.get('.pre-wrap').text()).toBe('Classic.');

      await buttonOf(page, 'Edit note').trigger('click');
      await page.get('article input').setValue('   ');
      await page.get('article form').trigger('submit');
      const request = backend.expectOne('/api/watchlist/m2');
      expect(request.json).toEqual({ note: null });
      request.flush(ALIEN);
      await flushPromises();
      expect(page.find('.pre-wrap').exists()).toBe(false);
    });

    it('edits the note of one movie at a time', async () => {
      const { page } = await openWith([HEAT, ALIEN]);

      await buttonOf(page, 'Edit note').trigger('click');
      await buttonOf(page, 'Add note').trigger('click');

      expect(page.findAll('article')[0].find('form').exists()).toBe(false);
      expect(page.findAll('article')[1].find('form').exists()).toBe(true);
    });

    it('stops editing without saving on Cancel or Escape', async () => {
      const { page } = await openWith([HEAT]);

      await buttonOf(page, 'Edit note').trigger('click');
      await buttonOf(page, 'Cancel').trigger('click');
      expect(page.find('form').exists()).toBe(false);
      expect(page.get('.pre-wrap').text()).toBe('Recommended by Alice.');

      await buttonOf(page, 'Edit note').trigger('click');
      await page.get('article input').trigger('keydown', { key: 'Escape' });
      expect(page.find('form').exists()).toBe(false);
    });

    it('shows why a note could not be saved, and keeps the field open', async () => {
      const { page } = await openWith([HEAT]);

      await buttonOf(page, 'Edit note').trigger('click');
      await page.get('article form').trigger('submit');
      backend
        .expectOne('/api/watchlist/m1')
        .flush({ detail: 'Something broke.' }, { status: 500, statusText: 'Error' });
      await flushPromises();

      expect(page.get('[role=alert]').text()).toContain('Something broke.');
      expect(page.find('article form').exists()).toBe(true);
      expect(page.get('article form button[type=submit]').classes()).not.toContain('is-loading');
    });
  });

  describe('removing a movie', () => {
    it('takes the movie off the watchlist, and shows the list again', async () => {
      const { page } = await openWith([HEAT, ALIEN]);

      await buttonOf(page, 'Remove').trigger('click');
      const request = backend.expectOne('/api/watchlist/m1');
      expect(request.method).toBe('DELETE');
      request.flush(null, { status: 204, statusText: 'No Content' });
      await flushPromises();

      listRequest().flush(paged([ALIEN]));
      await flushPromises();
      expect(page.findAll('article')).toHaveLength(1);
      expect(text(page)).toContain('1 movie to watch');
    });

    it('shows why a movie could not be removed', async () => {
      const { page } = await openWith([HEAT]);

      await buttonOf(page, 'Remove').trigger('click');
      backend
        .expectOne('/api/watchlist/m1')
        .flush({ detail: 'Something broke.' }, { status: 500, statusText: 'Error' });
      await flushPromises();

      expect(page.get('[role=alert]').text()).toContain('Something broke.');
      expect(page.findAll('article')).toHaveLength(1);
    });

    it('goes to the last page when the page is gone, e.g. after removing its only movie', async () => {
      const { page, router } = await openWith(
        [HEAT],
        '/watchlist?page=3',
        paged([HEAT], { page: 3, totalCount: 41 }),
      );

      await buttonOf(page, 'Remove').trigger('click');
      backend.expectOne('/api/watchlist/m1').flush(null, { status: 204, statusText: 'No Content' });
      await flushPromises();
      // 40 movies are left: two pages. The third one is empty.
      listRequest(3).flush(paged([], { page: 3, totalCount: 40 }));
      await flushPromises();

      expect(router.currentRoute.value.fullPath).toBe('/watchlist?page=2');
      listRequest(2).flush(paged([ALIEN], { page: 2, totalCount: 40 }));
      await flushPromises();
      expect(text(page)).toContain('40 movies to watch');
    });
  });
});
