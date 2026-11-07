import { flushPromises, type VueWrapper } from '@vue/test-utils';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { createApi } from '../../core/api/api';
import { createHttp } from '../../core/api/http';
import type { Genre, MovieSummary, PagedResponse } from '../../core/api/models';
import { movieListProps } from '../../route-props';
import { useFakeBackend } from '../../test/fake-backend';
import { mountRoute } from '../../test/services';
import MovieList from './MovieList.vue';

const GENRES: Genre[] = [
  { id: 18, name: 'Drama' },
  { id: 27, name: 'Horror' },
];

const ALIEN: MovieSummary = {
  id: 'm1',
  title: 'Alien',
  releaseDate: '1979-05-25',
  posterUrl: null,
  genres: [{ id: 27, name: 'Horror' }],
  latestRating: 9,
  watchCount: 3,
  onWatchlist: false,
};

const HEAT: MovieSummary = {
  id: 'm2',
  title: 'Heat',
  releaseDate: '1995-12-15',
  posterUrl: 'https://image/heat.jpg',
  genres: [],
  latestRating: null,
  watchCount: 0,
  onWatchlist: true,
};

const paged = (
  items: MovieSummary[],
  { page = 1, totalCount = items.length } = {},
): PagedResponse<MovieSummary> => ({ items, page, pageSize: 24, totalCount });

describe('MovieList', () => {
  const backend = useFakeBackend();

  afterEach(() => vi.useRealTimers());

  async function open(url = '/movies') {
    const { page, router } = await mountRoute(
      url,
      { path: '/movies', component: MovieList, props: movieListProps },
      { api: createApi(createHttp()) },
    );
    backend.expectOne('/api/genres').flush(GENRES);
    return { page, router };
  }

  const moviesRequest = (query: string) => backend.expectOne(`/api/movies?${query}`);
  const text = (page: VueWrapper) => page.text().replace(/\s+/g, ' ');

  it('shows the movies with their year, watch count, rating and watchlist tag', async () => {
    const { page } = await open();
    expect(page.find('progress').exists()).toBe(true);

    moviesRequest('sort=title&page=1&pageSize=24').flush(paged([ALIEN, HEAT]));
    await flushPromises();

    expect(page.find('progress').exists()).toBe(false);
    expect(text(page)).toContain('2 movies');
    const cards = page.findAll('a.card');
    expect(cards.map((card) => card.attributes('href'))).toEqual(['/movies/m1', '/movies/m2']);
    expect(cards[0].get('.has-text-weight-semibold').text()).toBe('Alien');
    expect(cards[0].get('.is-size-7').text()).toBe('1979 · watched 3×');
    expect(cards[0].get('.rating-stars').text()).toBe('★★★★½');
    expect(cards[0].find('.tag').exists()).toBe(false);
    expect(cards[1].get('.is-size-7').text()).toBe('1995'); // Not watched yet.
    expect(cards[1].find('.rating-stars').exists()).toBe(false);
    expect(cards[1].get('.tag').text()).toBe('Watchlist');
    expect(cards[1].get('img').attributes('src')).toBe('https://image/heat.jpg');
  });

  it('says "1 movie" for one movie', async () => {
    const { page } = await open();
    moviesRequest('sort=title&page=1&pageSize=24').flush(paged([ALIEN]));
    await flushPromises();

    expect(text(page)).toContain('1 movie');
    expect(text(page)).not.toContain('1 movies');
  });

  it('shows the filters of the URL, and asks for those movies', async () => {
    const { page } = await open(
      '/movies?search=alien&genreId=27&year=1979&sort=-releaseDate&page=2',
    );
    moviesRequest('search=alien&genreId=27&year=1979&sort=-releaseDate&page=2&pageSize=24').flush(
      paged([ALIEN], { page: 2, totalCount: 25 }),
    );
    await flushPromises();

    expect((page.get('#search').element as HTMLInputElement).value).toBe('alien');
    expect((page.get('#year').element as HTMLInputElement).value).toBe('1979');
    expect((page.get('#genre').element as HTMLSelectElement).value).toBe('27');
    expect((page.get('#sort').element as HTMLSelectElement).value).toBe('-releaseDate');
    expect(page.get('.pagination-link.is-current').text()).toBe('2');
  });

  it('ignores nonsense in the URL', async () => {
    const { page } = await open('/movies?sort=bogus&genreId=abc&year=&page=0');
    moviesRequest('sort=title&page=1&pageSize=24').flush(paged([]));
    await flushPromises();

    expect((page.get('#sort').element as HTMLSelectElement).value).toBe('title');
    expect((page.get('#genre').element as HTMLSelectElement).value).toBe('');
  });

  it('suggests to find a movie when there are none', async () => {
    const { page } = await open();
    moviesRequest('sort=title&page=1&pageSize=24').flush(paged([]));
    await flushPromises();

    expect(text(page)).toContain('No movies found. Find a movie above');
    expect(page.find('.notification button').exists()).toBe(false);
  });

  it('lets the user clear the filters when nothing matches them', async () => {
    const { page, router } = await open('/movies?search=nothing&page=1');
    moviesRequest('search=nothing&sort=title&page=1&pageSize=24').flush(paged([]));
    await flushPromises();

    await page.get('.notification button').trigger('click');
    await flushPromises();

    expect(router.currentRoute.value.fullPath).toBe('/movies');
    moviesRequest('sort=title&page=1&pageSize=24').flush(paged([ALIEN]));
    await flushPromises();
    expect(text(page)).toContain('1 movie');
  });

  it('shows why the movies cannot be loaded, until it is dismissed', async () => {
    const { page } = await open();
    moviesRequest('sort=title&page=1&pageSize=24').networkError();
    await flushPromises();

    expect(page.get('[role=alert]').text()).toContain('cannot be reached');
    expect(page.find('progress').exists()).toBe(false);

    await page.get('button.delete').trigger('click');
    expect(page.find('[role=alert]').exists()).toBe(false);
  });

  it('filters by genre, starting again at the first page', async () => {
    const { page, router } = await open('/movies?search=alien&page=3');
    moviesRequest('search=alien&sort=title&page=3&pageSize=24').flush(
      paged([ALIEN], { page: 3, totalCount: 60 }),
    );
    await flushPromises();

    await page.get('#genre').setValue('27');
    await flushPromises();

    expect(router.currentRoute.value.query).toEqual({ search: 'alien', genreId: '27' });
    moviesRequest('search=alien&genreId=27&sort=title&page=1&pageSize=24').flush(paged([ALIEN]));
    await flushPromises();
  });

  it('sorts and filters by year', async () => {
    const { page, router } = await open();
    moviesRequest('sort=title&page=1&pageSize=24').flush(paged([ALIEN]));
    await flushPromises();

    await page.get('#sort').setValue('-releaseDate');
    await flushPromises();
    moviesRequest('sort=-releaseDate&page=1&pageSize=24').flush(paged([ALIEN]));
    await flushPromises();

    await page.get('#year').setValue('1979');
    await page.get('#year').trigger('change');
    await flushPromises();
    moviesRequest('year=1979&sort=-releaseDate&page=1&pageSize=24').flush(paged([ALIEN]));
    await flushPromises();

    expect(router.currentRoute.value.query).toEqual({ sort: '-releaseDate', year: '1979' });
  });

  it('searches when the user has stopped typing, without piling up history entries', async () => {
    const { page, router } = await open('/movies?page=2');
    moviesRequest('sort=title&page=2&pageSize=24').flush(
      paged([ALIEN], { page: 2, totalCount: 30 }),
    );
    await flushPromises();
    vi.useFakeTimers({ toFake: ['setTimeout', 'clearTimeout'] });
    const replace = vi.spyOn(router, 'replace');

    await page.get('#search').setValue('he');
    await page.get('#search').setValue('heat ');
    await vi.advanceTimersByTimeAsync(200);
    expect(replace).not.toHaveBeenCalled();
    await vi.advanceTimersByTimeAsync(100);
    await flushPromises();

    expect(replace).toHaveBeenCalledOnce();
    expect(router.currentRoute.value.query).toEqual({ search: 'heat ' }); // Not trimmed.
    moviesRequest('search=heat%20&sort=title&page=1&pageSize=24'.replace('%20', '+')).flush(
      paged([HEAT]),
    );
    await flushPromises();
    expect((page.get('#search').element as HTMLInputElement).value).toBe('heat ');
  });

  it('keeps what the user types while the movies are still loading', async () => {
    const { page } = await open();
    await page.get('#search').setValue('he');

    moviesRequest('sort=title&page=1&pageSize=24').flush(paged([ALIEN]));
    await flushPromises();

    expect((page.get('#search').element as HTMLInputElement).value).toBe('he');
  });

  it('goes to another page, keeping the filters', async () => {
    const { page, router } = await open('/movies?search=alien');
    moviesRequest('search=alien&sort=title&page=1&pageSize=24').flush(
      paged([ALIEN], { totalCount: 60 }),
    );
    await flushPromises();

    await page.findAll('.pagination-link')[1].trigger('click');
    await flushPromises();

    expect(router.currentRoute.value.query).toEqual({ search: 'alien', page: '2' });
    moviesRequest('search=alien&sort=title&page=2&pageSize=24').flush(
      paged([HEAT], { page: 2, totalCount: 60 }),
    );
    await flushPromises();
    expect(text(page)).toContain('Heat');
  });

  it('shows the answer to the latest request only, and says it is loading meanwhile', async () => {
    const { page, router } = await open();
    const outdated = moviesRequest('sort=title&page=1&pageSize=24');

    await router.push('/movies?sort=-title');
    await flushPromises();
    const latest = moviesRequest('sort=-title&page=1&pageSize=24');
    latest.flush(paged([HEAT]));
    await flushPromises();
    outdated.flush(paged([ALIEN, HEAT]));
    await flushPromises();

    expect(text(page)).toContain('1 movie');
    expect(text(page)).not.toContain('loading');
  });

  it('opens the movie that is picked from the search', async () => {
    const { page, router } = await open();
    moviesRequest('sort=title&page=1&pageSize=24').flush(paged([ALIEN]));
    await flushPromises();
    vi.useFakeTimers({ toFake: ['setTimeout', 'clearTimeout'] });

    await page.get('#find').setValue('hea');
    await vi.advanceTimersByTimeAsync(250);
    backend.expectOne('/api/movies/search?query=hea').flush({
      tmdbAvailable: true,
      items: [
        {
          movieId: 'm2',
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
    await flushPromises();

    expect(router.currentRoute.value.path).toBe('/movies/m2');
  });
});
