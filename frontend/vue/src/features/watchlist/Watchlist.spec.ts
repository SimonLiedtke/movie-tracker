import { flushPromises, type VueWrapper } from '@vue/test-utils';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { createApi } from '../../core/api/api';
import { createHttp } from '../../core/api/http';
import type { Movie, WatchlistPage, WatchlistItem } from '../../core/api/models';
import { watchlistProps } from '../../route-props';
import { useFakeBackend } from '../../test/fake-backend';
import { mountRoute } from '../../test/services';
import Watchlist from './Watchlist.vue';

const item = (id: string, title: string, changes: Partial<WatchlistItem> = {}): WatchlistItem => ({
  movie: { id, title, releaseDate: '1995-12-15', posterUrl: null },
  addedAt: '2026-11-05T21:30:00Z',
  note: null,
  tags: [],
  genres: [],
  runtimeMinutes: null,
  ...changes,
});

const HEAT = item('m1', 'Heat', {
  note: 'Recommended by Alice.',
  genres: [{ id: 18, name: 'Drama' }],
  runtimeMinutes: 170,
  movie: {
    id: 'm1',
    title: 'Heat',
    releaseDate: '1995-12-15',
    posterUrl: 'https://image/heat.jpg',
  },
});
const ALIEN = item('m2', 'Alien', {
  runtimeMinutes: 117,
  genres: [
    { id: 27, name: 'Horror' },
    { id: 878, name: 'Science Fiction' },
  ],
  movie: { id: 'm2', title: 'Alien', releaseDate: null, posterUrl: null },
});

const paged = (
  items: WatchlistItem[],
  {
    page = 1,
    totalCount = items.length,
    unfilteredCount,
  }: { page?: number; totalCount?: number; unfilteredCount?: number } = {},
): WatchlistPage => ({
  items,
  page,
  pageSize: 20,
  totalCount,
  unfilteredCount: unfilteredCount ?? totalCount,
});

describe('Watchlist', () => {
  const backend = useFakeBackend();

  afterEach(() => vi.useRealTimers());

  const GENRES = [
    { id: 27, name: 'Horror' },
    { id: 878, name: 'Science Fiction' },
    { id: 18, name: 'Drama' },
  ];

  /** Opens the page and answers the request for the genres of the filter. */
  async function open(url = '/watchlist') {
    const opened = await mountRoute(
      url,
      { path: '/watchlist', component: Watchlist, props: watchlistProps },
      { api: createApi(createHttp()) },
    );
    backend.expectOne('/api/genres').flush(GENRES);
    await flushPromises();
    return opened;
  }

  /** The request for a page; `filter` is the rest of the query string, e.g. `&genreId=27&genreMatch=any`. */
  const listRequest = (page = 1, filter = '') =>
    backend.expectOne(`/api/watchlist?page=${page}&pageSize=20${filter}`);
  const text = (page: VueWrapper) => page.text().replace(/\s+/g, ' ');
  const buttonOf = (page: VueWrapper, label: string, index = 0) =>
    page.findAll('button, a.button').filter((b) => b.text() === label)[index];

  async function openWith(
    items: WatchlistItem[],
    url = '/watchlist',
    response = paged(items),
    filter = '',
  ) {
    const opened = await open(url);
    listRequest(response.page, filter).flush(response);
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

  describe('title search', () => {
    const field = (page: VueWrapper) => page.get<HTMLInputElement>('#search');

    /** Types the text, and lets the debounce time pass. */
    async function type(page: VueWrapper, value: string) {
      await field(page).setValue(value);
      await vi.advanceTimersByTimeAsync(350);
      await flushPromises();
    }

    beforeEach(() => {
      vi.useFakeTimers({ toFake: ['setTimeout', 'clearTimeout'] });
    });

    it('lists the movies with the text in their title, once the user has stopped typing', async () => {
      const { page, router } = await openWith([HEAT, ALIEN]);

      await field(page).setValue('al');
      await vi.advanceTimersByTimeAsync(100);
      await field(page).setValue('ali');
      await vi.advanceTimersByTimeAsync(100);
      await field(page).setValue('alien');
      // Nothing yet: the user may still be typing.
      expect(router.currentRoute.value.fullPath).toBe('/watchlist');

      await vi.advanceTimersByTimeAsync(350);
      await flushPromises();

      expect(router.currentRoute.value.fullPath).toBe('/watchlist?search=alien');
      listRequest(1, '&search=alien').flush(paged([ALIEN], { unfilteredCount: 52 }));
      await flushPromises();
      expect(text(page)).toContain('1 of 52 movies to watch matching "alien"');
      expect(field(page).element.value).toBe('alien');
      expect(buttonOf(page, 'Clear filter')).toBeDefined();
    });

    it('starts again at page 1, and keeps the other filters', async () => {
      const { page, router } = await openWith(
        [HEAT],
        '/watchlist?genreId=27&page=2',
        paged([HEAT], { page: 2, totalCount: 45 }),
        '&genreId=27&genreMatch=any',
      );
      // The request above is for page 2; the search starts at page 1.
      await type(page, 'heat');

      expect(router.currentRoute.value.fullPath).toBe('/watchlist?genreId=27&search=heat');
      listRequest(1, '&genreId=27&genreMatch=any&search=heat').flush(paged([HEAT]));
    });

    it('shows the search of the URL, and sends it without the spaces around it', async () => {
      const { page } = await openWith(
        [ALIEN],
        '/watchlist?search=alien%20',
        paged([ALIEN]),
        '&search=alien',
      );

      expect(field(page).element.value).toBe('alien ');
      expect(text(page)).toContain('1 of 1 movie to watch matching "alien"');
    });

    it('does not filter by spaces only, and removes the search from the URL when emptied', async () => {
      const { page, router } = await openWith([HEAT, ALIEN]);

      await type(page, '   ');
      expect(router.currentRoute.value.fullPath).toBe('/watchlist');
      expect(buttonOf(page, 'Clear filter')).toBeUndefined();

      await type(page, 'alien');
      listRequest(1, '&search=alien').flush(paged([ALIEN]));
      await flushPromises();
      await type(page, '');

      expect(router.currentRoute.value.fullPath).toBe('/watchlist');
      listRequest().flush(paged([HEAT, ALIEN]));
    });

    it('is cleared with the other filters, and says when no title matches', async () => {
      const { page, router } = await openWith(
        [],
        '/watchlist?search=zzz',
        paged([], { unfilteredCount: 3 }),
        '&search=zzz',
      );
      expect(text(page)).toContain('0 of 3 movies to watch matching "zzz"');
      expect(text(page)).toContain('No movie on your watchlist matches the filter.');

      await buttonOf(page, 'Clear filter').trigger('click');
      await flushPromises();

      expect(router.currentRoute.value.fullPath).toBe('/watchlist');
      listRequest().flush(paged([HEAT, ALIEN]));
      await flushPromises();
      expect(field(page).element.value).toBe('');
    });

    it('is combined with the genres in the description', async () => {
      const { page } = await openWith(
        [ALIEN],
        '/watchlist?search=alien&genreId=27',
        paged([ALIEN], { unfilteredCount: 9 }),
        '&genreId=27&genreMatch=any&search=alien',
      );

      expect(text(page)).toContain(
        '1 of 9 movies to watch matching "alien", with any of the selected genres',
      );
    });
  });

  describe('runtime of a movie', () => {
    const line = (article: ReturnType<VueWrapper['findAll']>[number]) =>
      article.get('.is-size-7.has-text-grey').text().replace(/\s+/g, ' ');

    it('is shown after the date added, and left out if unknown', async () => {
      const { page } = await openWith([HEAT, ALIEN, item('m3', 'Unknown')]);

      const articles = page.findAll('article');
      expect(line(articles[0])).toBe('Added 2026-11-05 · 170 min');
      expect(line(articles[1])).toBe('Added 2026-11-05 · 117 min');
      expect(line(articles[2])).toBe('Added 2026-11-05');
    });
  });

  describe('genres of a movie', () => {
    const tags = (article: Pick<VueWrapper, 'findAll'>) => article.findAll('.tags a.tag');

    it('shows them as tags like the movie page, without tags for a movie that has no genres', async () => {
      const { page } = await openWith([HEAT, ALIEN, item('m3', 'Unknown')]);

      const articles = page.findAll('article');
      expect(tags(articles[0]).map((t) => t.text())).toEqual(['Drama']);
      expect(tags(articles[1]).map((t) => t.text())).toEqual(['Horror', 'Science Fiction']);
      expect(articles[1].get('a.tag').classes()).toEqual(
        expect.arrayContaining(['tag', 'is-link', 'is-light']),
      );
      expect(articles[2].find('.tags').exists()).toBe(false);
    });

    it('filters by the genre when clicked, leaving the match mode alone, from page 1', async () => {
      const { page, router } = await openWith(
        [ALIEN],
        '/watchlist?genreId=18&genreMatch=all&page=2',
        paged([ALIEN], { page: 2, totalCount: 21 }),
        '&genreId=18&genreMatch=all',
      );
      const horror = tags(page.get('article')).find((t) => t.text() === 'Horror')!;
      expect(horror.attributes('href')).toBe('/watchlist?genreId=27&genreMatch=all');

      await horror.trigger('click');
      await flushPromises();

      expect(router.currentRoute.value.fullPath).toBe('/watchlist?genreId=27&genreMatch=all');
      listRequest(1, '&genreId=27&genreMatch=all').flush(paged([ALIEN]));
      await flushPromises();
      expect(
        page
          .findAll('[aria-labelledby=genre-label] button[aria-pressed=true]')
          .map((b) => b.text()),
      ).toEqual(['Horror']);
    });

    it('shows the genres of the filter highlighted', async () => {
      const { page } = await openWith(
        [ALIEN],
        '/watchlist?genreId=27',
        paged([ALIEN]),
        '&genreId=27&genreMatch=any',
      );

      const [horror, scienceFiction] = tags(page.get('article'));
      expect(horror.classes()).not.toContain('is-light');
      expect(scienceFiction.classes()).toContain('is-light');
    });
  });

  describe('runtime and date filters', () => {
    const input = (
      page: VueWrapper,
      id:
        'released-from' | 'released-to' | 'added-from' | 'added-to' | 'runtime-from' | 'runtime-to',
    ) => page.get<HTMLInputElement>(`#${id}`);

    /** Enters the date, and answers the request that follows. */
    async function enter(
      page: VueWrapper,
      router: { currentRoute: { value: { fullPath: string } } },
      id:
        'released-from' | 'released-to' | 'added-from' | 'added-to' | 'runtime-from' | 'runtime-to',
      value: string,
      expected: { url: string; filter: string },
      response: WatchlistPage,
    ) {
      await input(page, id).setValue(value);
      await input(page, id).trigger('change');
      await flushPromises();
      expect(router.currentRoute.value.fullPath).toBe(expected.url);
      listRequest(1, expected.filter).flush(response);
      await flushPromises();
    }

    it('has no dates, and no filter, to begin with', async () => {
      const { page } = await openWith([HEAT]);

      expect(input(page, 'released-from').element.value).toBe('');
      expect(input(page, 'released-to').element.value).toBe('');
      expect(buttonOf(page, 'Clear filter')).toBeUndefined();
    });

    it('lists the movies released from a day on, and says so', async () => {
      const { page, router } = await openWith([HEAT, ALIEN]);

      await enter(
        page,
        router,
        'released-from',
        '1979-05-25',
        { url: '/watchlist?releasedFrom=1979-05-25', filter: '&releasedFrom=1979-05-25' },
        paged([ALIEN], { unfilteredCount: 52 }),
      );

      expect(text(page)).toContain('1 of 52 movies to watch released from May 25, 1979');
      expect(input(page, 'released-to').attributes('min')).toBe('1979-05-25');
      expect(buttonOf(page, 'Clear filter')).toBeDefined();
    });

    it('lists the movies released until a day, and in a period', async () => {
      const { page, router } = await openWith([HEAT, ALIEN]);

      await enter(
        page,
        router,
        'released-to',
        '1989-12-31',
        { url: '/watchlist?releasedTo=1989-12-31', filter: '&releasedTo=1989-12-31' },
        paged([ALIEN], { unfilteredCount: 52 }),
      );
      expect(text(page)).toContain('1 of 52 movies to watch released until December 31, 1989');
      expect(input(page, 'released-from').attributes('max')).toBe('1989-12-31');

      await enter(
        page,
        router,
        'released-from',
        '1979-01-01',
        {
          url: '/watchlist?releasedTo=1989-12-31&releasedFrom=1979-01-01',
          filter: '&releasedFrom=1979-01-01&releasedTo=1989-12-31',
        },
        paged([ALIEN], { unfilteredCount: 52 }),
      );
      expect(text(page)).toContain(
        '1 of 52 movies to watch released from January 1, 1979 to December 31, 1989',
      );
    });

    it('removes a date that is emptied', async () => {
      const { page, router } = await openWith(
        [ALIEN],
        '/watchlist?releasedFrom=1979-01-01',
        paged([ALIEN]),
        '&releasedFrom=1979-01-01',
      );

      await enter(
        page,
        router,
        'released-from',
        '',
        { url: '/watchlist', filter: '' },
        paged([HEAT, ALIEN]),
      );

      expect(buttonOf(page, 'Clear filter')).toBeUndefined();
    });

    it('shows the dates of the URL, and sends them with the genres and the page', async () => {
      const { page, router } = await openWith(
        [ALIEN],
        '/watchlist?genreId=27&genreMatch=all&releasedFrom=1979-01-01&releasedTo=1989-12-31',
        paged([ALIEN], { unfilteredCount: 9 }),
        '&genreId=27&genreMatch=all&releasedFrom=1979-01-01&releasedTo=1989-12-31',
      );

      expect(input(page, 'released-from').element.value).toBe('1979-01-01');
      expect(input(page, 'released-to').element.value).toBe('1989-12-31');
      expect(text(page)).toContain(
        '1 of 9 movies to watch with all of the selected genres, released from January 1, 1979 to December 31, 1989',
      );
      expect(router.currentRoute.value.fullPath).toContain('releasedFrom=1979-01-01');
    });

    it('keeps the dates when the genres change or the page, and starts at page 1', async () => {
      const { page, router } = await openWith(
        [HEAT],
        '/watchlist?releasedFrom=1979-01-01',
        paged([HEAT], { totalCount: 45 }),
        '&releasedFrom=1979-01-01',
      );

      await page.findAll('.pagination-link')[2].trigger('click');
      await flushPromises();
      expect(router.currentRoute.value.fullPath).toBe('/watchlist?releasedFrom=1979-01-01&page=3');
      listRequest(3, '&releasedFrom=1979-01-01').flush(paged([ALIEN], { page: 3, totalCount: 45 }));
      await flushPromises();

      await page.findAll('[aria-labelledby=genre-label] button')[0].trigger('click');
      await flushPromises();
      expect(router.currentRoute.value.fullPath).toBe(
        '/watchlist?releasedFrom=1979-01-01&genreId=27',
      );
      listRequest(1, '&genreId=27&genreMatch=any&releasedFrom=1979-01-01').flush(paged([ALIEN]));
    });

    it('clears the dates together with the genres', async () => {
      const { page, router } = await openWith(
        [ALIEN],
        '/watchlist?genreId=27&releasedFrom=1979-01-01&releasedTo=1989-12-31',
        paged([ALIEN]),
        '&genreId=27&genreMatch=any&releasedFrom=1979-01-01&releasedTo=1989-12-31',
      );

      await buttonOf(page, 'Clear filter').trigger('click');
      await flushPromises();

      expect(router.currentRoute.value.fullPath).toBe('/watchlist');
      listRequest().flush(paged([HEAT, ALIEN]));
      await flushPromises();
      expect(input(page, 'released-from').element.value).toBe('');
      expect(input(page, 'released-to').element.value).toBe('');
    });

    it('lists the movies added from a day on, until a day, and in a period, and says so', async () => {
      const { page, router } = await openWith([HEAT, ALIEN]);

      await enter(
        page,
        router,
        'added-from',
        '2026-11-01',
        { url: '/watchlist?addedFrom=2026-11-01', filter: '&addedFrom=2026-11-01' },
        paged([ALIEN], { unfilteredCount: 52 }),
      );
      expect(text(page)).toContain('1 of 52 movies to watch added from November 1, 2026');
      expect(input(page, 'added-to').attributes('min')).toBe('2026-11-01');

      await enter(
        page,
        router,
        'added-to',
        '2026-11-05',
        {
          url: '/watchlist?addedFrom=2026-11-01&addedTo=2026-11-05',
          filter: '&addedFrom=2026-11-01&addedTo=2026-11-05',
        },
        paged([ALIEN], { unfilteredCount: 52 }),
      );
      expect(text(page)).toContain('added from November 1, 2026 to November 5, 2026');
      expect(input(page, 'added-from').attributes('max')).toBe('2026-11-05');
    });

    it('lists the movies added until a day', async () => {
      const { page, router } = await openWith([HEAT, ALIEN]);

      await enter(
        page,
        router,
        'added-to',
        '2026-11-05',
        { url: '/watchlist?addedTo=2026-11-05', filter: '&addedTo=2026-11-05' },
        paged([ALIEN], { unfilteredCount: 52 }),
      );

      expect(text(page)).toContain('1 of 52 movies to watch added until November 5, 2026');
    });

    it('combines both periods with the genres, and shows them from the URL', async () => {
      const { page } = await openWith(
        [ALIEN],
        '/watchlist?genreId=27&releasedFrom=1979-01-01&addedTo=2026-11-05',
        paged([ALIEN], { unfilteredCount: 9 }),
        '&genreId=27&genreMatch=any&releasedFrom=1979-01-01&addedTo=2026-11-05',
      );

      expect(input(page, 'released-from').element.value).toBe('1979-01-01');
      expect(input(page, 'added-from').element.value).toBe('');
      expect(input(page, 'added-to').element.value).toBe('2026-11-05');
      expect(text(page)).toContain(
        '1 of 9 movies to watch with any of the selected genres, released from January 1, 1979, added until November 5, 2026',
      );
    });

    it('clears the added dates, too', async () => {
      const { page, router } = await openWith(
        [ALIEN],
        '/watchlist?addedFrom=2026-11-01&addedTo=2026-11-05',
        paged([ALIEN]),
        '&addedFrom=2026-11-01&addedTo=2026-11-05',
      );

      await buttonOf(page, 'Clear filter').trigger('click');
      await flushPromises();

      expect(router.currentRoute.value.fullPath).toBe('/watchlist');
      listRequest().flush(paged([HEAT, ALIEN]));
      await flushPromises();
      expect(input(page, 'added-from').element.value).toBe('');
      expect(input(page, 'added-to').element.value).toBe('');
    });

    describe('runtime', () => {
      const presetButtons = (page: VueWrapper) =>
        page.findAll('[aria-labelledby=runtime-label] button');
      const preset = (page: VueWrapper, label: string) =>
        presetButtons(page).find((b) => b.text() === label)!;
      const pressed = (page: VueWrapper) =>
        presetButtons(page)
          .filter((b) => b.attributes('aria-pressed') === 'true')
          .map((b) => b.text());

      async function pick(
        page: VueWrapper,
        router: { currentRoute: { value: { fullPath: string } } },
        label: string,
        expected: { url: string; filter: string },
        response: WatchlistPage,
      ) {
        await preset(page, label).trigger('click');
        await flushPromises();
        expect(router.currentRoute.value.fullPath).toBe(expected.url);
        listRequest(1, expected.filter).flush(response);
        await flushPromises();
      }

      it('offers quick picks for the usual lengths, and fields for any other, none set to begin with', async () => {
        const { page } = await openWith([HEAT]);

        expect(presetButtons(page).map((b) => b.text())).toEqual([
          'Under 90 min',
          '90 to 120 min',
          'Over 120 min',
        ]);
        expect(pressed(page)).toEqual([]);
        expect(input(page, 'runtime-from').element.value).toBe('');
        expect(input(page, 'runtime-to').element.value).toBe('');
        expect(input(page, 'runtime-from').attributes('min')).toBe('1');
      });

      it('lists the movies of exactly the entered length range, and says so', async () => {
        const { page, router } = await openWith([HEAT, ALIEN]);

        await enter(
          page,
          router,
          'runtime-from',
          '23',
          { url: '/watchlist?runtimeFrom=23', filter: '&runtimeFrom=23' },
          paged([ALIEN], { unfilteredCount: 52 }),
        );
        expect(text(page)).toContain('1 of 52 movies to watch runtime of at least 23 minutes');

        await enter(
          page,
          router,
          'runtime-to',
          '148',
          {
            url: '/watchlist?runtimeFrom=23&runtimeTo=148',
            filter: '&runtimeFrom=23&runtimeTo=148',
          },
          paged([ALIEN], { unfilteredCount: 52 }),
        );
        expect(text(page)).toContain('1 of 52 movies to watch runtime of 23 to 148 minutes');
        expect(pressed(page)).toEqual([]);
      });

      it('lists the movies that are at most as long as entered', async () => {
        const { page, router } = await openWith([HEAT, ALIEN]);

        await enter(
          page,
          router,
          'runtime-to',
          '148',
          { url: '/watchlist?runtimeTo=148', filter: '&runtimeTo=148' },
          paged([ALIEN], { unfilteredCount: 52 }),
        );

        expect(text(page)).toContain('1 of 52 movies to watch runtime of at most 148 minutes');
      });

      it('selects a quick pick, and deselects it with a second click', async () => {
        const { page, router } = await openWith([HEAT, ALIEN]);

        await pick(
          page,
          router,
          'Under 90 min',
          { url: '/watchlist?runtimeTo=89', filter: '&runtimeTo=89' },
          paged([ALIEN]),
        );
        expect(pressed(page)).toEqual(['Under 90 min']);
        expect(input(page, 'runtime-to').element.value).toBe('89');
        expect(input(page, 'runtime-from').element.value).toBe('');

        await pick(page, router, 'Under 90 min', { url: '/watchlist', filter: '' }, paged([HEAT]));
        expect(pressed(page)).toEqual([]);
        expect(input(page, 'runtime-to').element.value).toBe('');
      });

      it('replaces the runtime of another quick pick or of the fields', async () => {
        const { page, router } = await openWith(
          [ALIEN],
          '/watchlist?runtimeFrom=23&runtimeTo=148',
          paged([ALIEN]),
          '&runtimeFrom=23&runtimeTo=148',
        );

        await pick(
          page,
          router,
          '90 to 120 min',
          {
            url: '/watchlist?runtimeFrom=90&runtimeTo=120',
            filter: '&runtimeFrom=90&runtimeTo=120',
          },
          paged([ALIEN]),
        );
        expect(pressed(page)).toEqual(['90 to 120 min']);

        // "Over 120 min" has no upper end: the old one is removed.
        await pick(
          page,
          router,
          'Over 120 min',
          { url: '/watchlist?runtimeFrom=121', filter: '&runtimeFrom=121' },
          paged([HEAT]),
        );
        expect(pressed(page)).toEqual(['Over 120 min']);
        expect(input(page, 'runtime-to').element.value).toBe('');
      });

      it('highlights the quick pick if the fields have the same values', async () => {
        const { page } = await openWith(
          [ALIEN],
          '/watchlist?runtimeFrom=90&runtimeTo=120',
          paged([ALIEN]),
          '&runtimeFrom=90&runtimeTo=120',
        );

        expect(pressed(page)).toEqual(['90 to 120 min']);
      });

      it('ignores what is not a number of minutes in the URL', async () => {
        const { page } = await openWith(
          [ALIEN],
          '/watchlist?runtimeFrom=abc&runtimeTo=-5',
          paged([ALIEN]),
        );

        expect(input(page, 'runtime-from').element.value).toBe('');
        expect(buttonOf(page, 'Clear filter')).toBeUndefined();
      });

      it('keeps it with the other filters, and clears it with them', async () => {
        const { page, router } = await openWith(
          [ALIEN],
          '/watchlist?genreId=27&runtimeFrom=90&releasedTo=1989-12-31',
          paged([ALIEN], { unfilteredCount: 9 }),
          '&genreId=27&genreMatch=any&releasedTo=1989-12-31&runtimeFrom=90',
        );
        expect(text(page)).toContain(
          '1 of 9 movies to watch with any of the selected genres, runtime of at least 90 minutes, released until December 31, 1989',
        );

        await buttonOf(page, 'Clear filter').trigger('click');
        await flushPromises();

        expect(router.currentRoute.value.fullPath).toBe('/watchlist');
        listRequest().flush(paged([HEAT, ALIEN]));
        await flushPromises();
        expect(input(page, 'runtime-from').element.value).toBe('');
      });
    });

    it('says when no movie was released in the period', async () => {
      const { page } = await openWith(
        [],
        '/watchlist?releasedTo=1900-01-01',
        paged([], { unfilteredCount: 3 }),
        '&releasedTo=1900-01-01',
      );

      expect(text(page)).toContain('0 of 3 movies to watch released until January 1, 1900');
      expect(text(page)).toContain('No movie on your watchlist matches the filter.');
    });
  });

  describe('genre filter', () => {
    const genreButtons = (page: VueWrapper) => page.findAll('[aria-labelledby=genre-label] button');
    const genreButton = (page: VueWrapper, name: string) =>
      genreButtons(page).find((b) => b.text() === name)!;
    const matchButton = (page: VueWrapper, label: string) =>
      page.findAll('[aria-labelledby=genre-match-label] button').find((b) => b.text() === label)!;
    const selectedGenres = (page: VueWrapper) =>
      genreButtons(page)
        .filter((b) => b.attributes('aria-pressed') === 'true')
        .map((b) => b.text());

    /** Clicks the button, and answers the request that follows with the items. */
    async function click(
      button: { trigger: (event: string) => Promise<unknown> },
      url: string,
      expected: { page?: number; filter: string },
      response: WatchlistPage,
      router: { currentRoute: { value: { fullPath: string } } },
    ) {
      await button.trigger('click');
      await flushPromises();
      expect(router.currentRoute.value.fullPath).toBe(url);
      listRequest(expected.page, expected.filter).flush(response);
      await flushPromises();
    }

    it('offers every genre, none selected, and "any of them" to combine them', async () => {
      const { page } = await openWith([HEAT]);

      expect(genreButtons(page).map((b) => b.text())).toEqual([
        'Horror',
        'Science Fiction',
        'Drama',
      ]);
      expect(selectedGenres(page)).toEqual([]);
      expect(matchButton(page, 'Any of them').attributes('aria-pressed')).toBe('true');
      expect(matchButton(page, 'All of them').attributes('aria-pressed')).toBe('false');
      expect(buttonOf(page, 'Clear filter')).toBeUndefined();
    });

    it('selects a genre, which loads the matching movies', async () => {
      const { page, router } = await openWith([HEAT, ALIEN]);

      await click(
        genreButton(page, 'Horror'),
        '/watchlist?genreId=27',
        { filter: '&genreId=27&genreMatch=any' },
        paged([ALIEN], { unfilteredCount: 52 }),
        router,
      );

      expect(page.findAll('article')).toHaveLength(1);
      expect(selectedGenres(page)).toEqual(['Horror']);
      expect(text(page)).toContain('1 of 52 movies to watch with any of the selected genres');
      expect(buttonOf(page, 'Clear filter')).toBeDefined();
    });

    it('counts the movies of the filter and all movies on the watchlist, so that its effect is clear', async () => {
      const { page } = await openWith(
        [ALIEN],
        '/watchlist?genreId=27&genreId=878&genreMatch=all',
        paged([ALIEN], { totalCount: 14, unfilteredCount: 52 }),
        '&genreId=27&genreId=878&genreMatch=all',
      );

      expect(text(page)).toContain('14 of 52 movies to watch with all of the selected genres');
    });

    it('says "of 1 movie" if the watchlist has one movie only, and "0 of" if none matches', async () => {
      const { page } = await openWith(
        [],
        '/watchlist?genreId=18',
        paged([], { unfilteredCount: 1 }),
        '&genreId=18&genreMatch=any',
      );

      expect(text(page)).toContain('0 of 1 movie to watch with any of the selected genres');
    });

    it('shows no "of" without a filter', async () => {
      const { page } = await openWith([HEAT, ALIEN]);

      expect(text(page)).toContain('2 movies to watch');
      expect(text(page)).not.toMatch(/\d of \d/);
    });

    it('selects several genres, and deselects one again', async () => {
      const { page, router } = await openWith([HEAT, ALIEN]);

      await click(
        genreButton(page, 'Horror'),
        '/watchlist?genreId=27',
        { filter: '&genreId=27&genreMatch=any' },
        paged([ALIEN]),
        router,
      );
      await click(
        genreButton(page, 'Drama'),
        '/watchlist?genreId=27&genreId=18',
        { filter: '&genreId=27&genreId=18&genreMatch=any' },
        paged([ALIEN, HEAT]),
        router,
      );
      expect(selectedGenres(page)).toEqual(['Horror', 'Drama']);

      await click(
        genreButton(page, 'Horror'),
        '/watchlist?genreId=18',
        { filter: '&genreId=18&genreMatch=any' },
        paged([HEAT]),
        router,
      );
      expect(selectedGenres(page)).toEqual(['Drama']);

      await click(
        genreButton(page, 'Drama'),
        '/watchlist',
        { filter: '' },
        paged([HEAT, ALIEN]),
        router,
      );
      expect(selectedGenres(page)).toEqual([]);
    });

    it('matches all genres instead of any, and back', async () => {
      const { page, router } = await openWith(
        [ALIEN],
        '/watchlist?genreId=27&genreId=878',
        paged([ALIEN]),
        '&genreId=27&genreId=878&genreMatch=any',
      );

      await click(
        matchButton(page, 'All of them'),
        '/watchlist?genreId=27&genreId=878&genreMatch=all',
        { filter: '&genreId=27&genreId=878&genreMatch=all' },
        paged([ALIEN]),
        router,
      );
      expect(matchButton(page, 'All of them').attributes('aria-pressed')).toBe('true');
      expect(matchButton(page, 'Any of them').attributes('aria-pressed')).toBe('false');
      expect(text(page)).toContain('with all of the selected genres');

      // "any" is the default, so it is not in the URL.
      await click(
        matchButton(page, 'Any of them'),
        '/watchlist?genreId=27&genreId=878',
        { filter: '&genreId=27&genreId=878&genreMatch=any' },
        paged([ALIEN, HEAT]),
        router,
      );
      expect(matchButton(page, 'Any of them').attributes('aria-pressed')).toBe('true');
    });

    it('does not load the list again when the match mode changes while no genre is selected', async () => {
      const { page, router } = await openWith([HEAT, ALIEN]);

      await matchButton(page, 'All of them').trigger('click');
      await flushPromises();

      // The URL and the buttons follow, but the list is the same: no request, no "loading".
      expect(router.currentRoute.value.fullPath).toBe('/watchlist?genreMatch=all');
      expect(matchButton(page, 'All of them').attributes('aria-pressed')).toBe('true');
      expect(text(page)).not.toContain('loading');
      expect(page.findAll('article')).toHaveLength(2);

      await matchButton(page, 'Any of them').trigger('click');
      await flushPromises();

      expect(router.currentRoute.value.fullPath).toBe('/watchlist');
      expect(text(page)).not.toContain('loading');
    });

    it('loads the list again when the match mode changes while genres are selected', async () => {
      const { page, router } = await openWith(
        [ALIEN],
        '/watchlist?genreId=27',
        paged([ALIEN]),
        '&genreId=27&genreMatch=any',
      );

      await click(
        matchButton(page, 'All of them'),
        '/watchlist?genreId=27&genreMatch=all',
        { filter: '&genreId=27&genreMatch=all' },
        paged([ALIEN]),
        router,
      );
    });

    it('shows the filter of the URL', async () => {
      const { page } = await openWith(
        [ALIEN],
        '/watchlist?genreId=27&genreId=878&genreMatch=all',
        paged([ALIEN]),
        '&genreId=27&genreId=878&genreMatch=all',
      );

      expect(selectedGenres(page)).toEqual(['Horror', 'Science Fiction']);
      expect(matchButton(page, 'All of them').attributes('aria-pressed')).toBe('true');
    });

    it('ignores what is not a genre ID in the URL, and duplicates', async () => {
      const { page } = await openWith(
        [ALIEN],
        '/watchlist?genreId=27&genreId=abc&genreId=27&genreMatch=some',
        paged([ALIEN]),
        '&genreId=27&genreMatch=any',
      );

      expect(selectedGenres(page)).toEqual(['Horror']);
    });

    it('does not send the match mode without genres', async () => {
      await openWith([HEAT], '/watchlist?genreMatch=all');
    });

    it('keeps the filter when going to another page, and starts at page 1 when it changes', async () => {
      const { page, router } = await openWith(
        [HEAT],
        '/watchlist?genreId=27',
        paged([HEAT], { totalCount: 45 }),
        '&genreId=27&genreMatch=any',
      );

      await click(
        page.findAll('.pagination-link')[2],
        '/watchlist?genreId=27&page=3',
        { page: 3, filter: '&genreId=27&genreMatch=any' },
        paged([ALIEN], { page: 3, totalCount: 45 }),
        router,
      );

      await click(
        genreButton(page, 'Drama'),
        '/watchlist?genreId=27&genreId=18',
        { filter: '&genreId=27&genreId=18&genreMatch=any' },
        paged([HEAT]),
        router,
      );
    });

    it('clears the filter, and starts at page 1', async () => {
      const { page, router } = await openWith(
        [ALIEN],
        '/watchlist?genreId=27&genreId=878&genreMatch=all&page=2',
        paged([ALIEN], { page: 2, totalCount: 21 }),
        '&genreId=27&genreId=878&genreMatch=all',
      );

      await click(
        buttonOf(page, 'Clear filter'),
        '/watchlist',
        { filter: '' },
        paged([HEAT, ALIEN]),
        router,
      );

      expect(selectedGenres(page)).toEqual([]);
      expect(matchButton(page, 'Any of them').attributes('aria-pressed')).toBe('true');
    });

    it('says when no movie matches, and offers to clear the filter', async () => {
      const { page, router } = await openWith(
        [],
        '/watchlist?genreId=18',
        paged([]),
        '&genreId=18&genreMatch=any',
      );

      expect(text(page)).toContain('No movie on your watchlist matches the filter.');
      expect(text(page)).not.toContain('Your watchlist is empty');

      await click(
        page.get('.notification button'),
        '/watchlist',
        { filter: '' },
        paged([HEAT]),
        router,
      );
    });

    it('still shows the watchlist if the genres cannot be loaded', async () => {
      const { page } = await mountRoute(
        '/watchlist',
        { path: '/watchlist', component: Watchlist, props: watchlistProps },
        { api: createApi(createHttp()) },
      );
      backend.expectOne('/api/genres').networkError();
      listRequest().flush(paged([HEAT]));
      await flushPromises();

      expect(page.findAll('article')).toHaveLength(1);
      expect(genreButtons(page)).toHaveLength(0);
    });
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
      expect(put.json).toEqual({ note: null, tags: [] });
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
      expect(request.json).toEqual({ note: 'Watch with Bob.', tags: [] });
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
      expect(request.json).toEqual({ note: null, tags: [] });
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
