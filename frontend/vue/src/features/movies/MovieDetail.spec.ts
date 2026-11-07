import { flushPromises, type VueWrapper } from '@vue/test-utils';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { createApi } from '../../core/api/api';
import { createHttp } from '../../core/api/http';
import type { DiaryEntry, Movie } from '../../core/api/models';
import { movieDetailProps } from '../../route-props';
import { useFakeBackend } from '../../test/fake-backend';
import { mountRoute } from '../../test/services';
import MovieDetail from './MovieDetail.vue';

const ALIEN: Movie = {
  id: 'm1',
  title: 'Alien',
  originalTitle: 'Alien',
  releaseDate: '1979-05-25',
  posterUrl: 'https://image/alien.jpg',
  runtimeMinutes: 117,
  overview: 'In space, no one can hear you scream.',
  genres: [
    { id: 27, name: 'Horror' },
    { id: 878, name: 'Science Fiction' },
  ],
  tmdbId: 348,
  latestRating: 9,
  watchCount: 2,
  lastWatchedOn: '2026-11-01',
  onWatchlist: false,
  createdAt: '2026-10-01T10:00:00Z',
};

/** A movie the user added: no TMDB ID, so it can be edited and deleted. */
const HOME_VIDEO: Movie = {
  ...ALIEN,
  id: 'm2',
  title: 'Home Video',
  originalTitle: 'Heimvideo',
  posterUrl: null,
  tmdbId: null,
  latestRating: null,
  watchCount: 0,
  lastWatchedOn: null,
  genres: [],
  overview: null,
  runtimeMinutes: null,
};

const entry = (id: string, changes: Partial<DiaryEntry> = {}): DiaryEntry => ({
  id,
  movie: { id: 'm1', title: 'Alien', releaseDate: '1979-05-25', posterUrl: null },
  watchedOn: '2026-11-01',
  rating: null,
  review: null,
  isRewatch: false,
  liked: false,
  createdAt: '2026-11-01T20:00:00Z',
  ...changes,
});

const ENTRIES = [
  entry('e1', { rating: 9, review: 'Still scary.', isRewatch: true, liked: true }),
  entry('e2', { watchedOn: '2026-10-01' }),
];

describe('MovieDetail', () => {
  const backend = useFakeBackend();

  afterEach(() => vi.restoreAllMocks());

  async function open(movie: Movie, entries: DiaryEntry[] = [], id = movie.id) {
    const { page, router } = await mountRoute(
      `/movies/${id}`,
      { path: '/movies/:id', component: MovieDetail, props: movieDetailProps },
      { api: createApi(createHttp()) },
    );
    await answer(movie, entries);
    return { page, router };
  }

  async function answer(movie: Movie, entries: DiaryEntry[]) {
    backend.expectOne(`/api/movies/${movie.id}`).flush(movie);
    backend.expectOne(`/api/movies/${movie.id}/diary-entries`).flush(entries);
    await flushPromises();
  }

  const text = (page: VueWrapper) => page.text().replace(/\s+/g, ' ');
  const button = (page: VueWrapper, label: string) =>
    page.findAll('button').find((b) => b.text() === label)!;

  it('shows the movie with its details', async () => {
    const { page } = await open(ALIEN, ENTRIES);

    expect(page.get('h1').text()).toBe('Alien (1979)');
    expect(page.get('img').attributes('src')).toBe('https://image/alien.jpg');
    expect(page.find('.subtitle').exists()).toBe(false); // The original title is the same.
    expect(page.get('.pre-wrap').text()).toBe('In space, no one can hear you scream.');
    expect(page.findAll('.tags.has-addons').map((tags) => tags.text())).toEqual([
      'Released1979-05-25',
      'Runtime117 min',
      'Watched2×',
      'Last watched2026-11-01',
      'TMDB348',
    ]);
    expect(page.get('a.tags').attributes('href')).toBe('https://www.themoviedb.org/movie/348');
    expect(page.get('.rating-stars').text()).toBe('★★★★½');
    expect(page.find('.tag.is-warning').exists()).toBe(false);
  });

  it('links the genres to the movies of that genre', async () => {
    const { page } = await open(ALIEN);

    const genres = page.findAll('.tags > a');
    expect(genres.map((genre) => genre.text())).toEqual(['Horror', 'Science Fiction']);
    expect(genres.map((genre) => genre.attributes('href'))).toEqual([
      '/movies?genreId=27',
      '/movies?genreId=878',
    ]);
  });

  it('leaves out what the movie does not have', async () => {
    const { page } = await open({ ...HOME_VIDEO, releaseDate: null });

    expect(page.get('h1').text()).toBe('Home Video');
    expect(page.get('.subtitle').text()).toBe('Original title: Heimvideo');
    expect(page.find('.pre-wrap').exists()).toBe(false);
    expect(page.find('.rating-stars').exists()).toBe(false);
    expect(page.findAll('.tags.has-addons').map((tags) => tags.text())).toEqual(['Watched0×']);
    expect(page.find('img').exists()).toBe(false);
  });

  it('lets the user change and delete only the movies they added themselves', async () => {
    const tmdb = await open(ALIEN);
    expect(tmdb.page.findAll('.buttons a, .buttons button').map((b) => b.text())).toEqual([
      'Log a viewing',
      'Add to watchlist',
    ]);
    expect(tmdb.page.find('.tag.is-warning').exists()).toBe(false);
    tmdb.page.unmount();

    const manual = await open(HOME_VIDEO);
    expect(manual.page.findAll('.buttons a, .buttons button').map((b) => b.text())).toEqual([
      'Log a viewing',
      'Add to watchlist',
      'Edit',
      'Delete',
    ]);
    expect(manual.page.get('.tag.is-warning').text()).toBe('Added manually');
    expect(manual.page.findAll('.buttons a').map((a) => a.attributes('href'))).toEqual([
      '/diary/new?movieId=m2',
      '/movies/m2/edit',
    ]);
  });

  it('lists the viewings', async () => {
    const { page } = await open(ALIEN, ENTRIES);

    const rows = page.findAll('tbody tr');
    expect(rows).toHaveLength(2);
    expect(rows[0].findAll('td').map((cell) => cell.text().replace(/\s+/g, ' '))).toEqual([
      '2026-11-01 ↻♥',
      '★★★★½',
      'Still scary.',
      'Edit Delete',
    ]);
    expect(rows[1].findAll('td')[0].text()).toBe('2026-10-01');
    expect(rows[0].get('a').attributes('href')).toBe('/diary/e1/edit');
  });

  it('says when the movie has not been watched yet', async () => {
    const { page } = await open(ALIEN, []);

    expect(page.find('table').exists()).toBe(false);
    expect(text(page)).toContain('Not watched yet.');
  });

  it('says so when the movie does not exist', async () => {
    const { page } = await mountRoute(
      '/movies/gone',
      { path: '/movies/:id', component: MovieDetail, props: movieDetailProps },
      { api: createApi(createHttp()) },
    );
    expect(page.find('progress').exists()).toBe(true);

    backend
      .expectOne('/api/movies/gone')
      .flush({ title: 'Not Found' }, { status: 404, statusText: 'Not Found' });
    backend.expectOne('/api/movies/gone/diary-entries').flush([]);
    await flushPromises();

    expect(page.get('.notification').text()).toContain('This movie does not exist');
    expect(page.get('.notification a').attributes('href')).toBe('/movies');
    expect(page.find('[role=alert]').exists()).toBe(false);
    expect(page.find('progress').exists()).toBe(false);
  });

  it('shows why the movie cannot be loaded', async () => {
    const { page } = await mountRoute(
      '/movies/m1',
      { path: '/movies/:id', component: MovieDetail, props: movieDetailProps },
      { api: createApi(createHttp()) },
    );

    backend.expectOne('/api/movies/m1').networkError();
    backend.expectOne('/api/movies/m1/diary-entries').networkError();
    await flushPromises();

    expect(page.get('[role=alert]').text()).toContain('cannot be reached');
    expect(page.find('.notification.is-warning').exists()).toBe(false);
  });

  it('loads another movie when the URL changes', async () => {
    const { page, router } = await open(ALIEN);

    await router.push('/movies/m2');
    await answer(HOME_VIDEO, []);

    expect(page.get('h1').text()).toBe('Home Video (1979)');
  });

  it('adds the movie to the watchlist, and takes it off again', async () => {
    const { page } = await open(ALIEN);

    await button(page, 'Add to watchlist').trigger('click');
    expect(button(page, 'Add to watchlist').attributes('disabled')).toBeDefined(); // Busy.
    const put = backend.expectOne('/api/watchlist/m1');
    expect(put.method).toBe('PUT');
    expect(put.json).toEqual({ note: null });
    put.flush({});
    await flushPromises();

    const remove = button(page, 'Remove from watchlist');
    expect(remove.attributes('disabled')).toBeUndefined();
    expect(remove.classes()).toContain('is-light');
    await remove.trigger('click');
    const del = backend.expectOne('/api/watchlist/m1');
    expect(del.method).toBe('DELETE');
    del.flush(null, { status: 204, statusText: 'No Content' });
    await flushPromises();

    expect(button(page, 'Add to watchlist').classes()).toContain('is-info');
  });

  it('shows why a change did not work, and lets the user try again', async () => {
    const { page } = await open(ALIEN);

    await button(page, 'Add to watchlist').trigger('click');
    backend
      .expectOne('/api/watchlist/m1')
      .flush({ detail: 'Something broke.' }, { status: 500, statusText: 'Error' });
    await flushPromises();

    expect(page.get('[role=alert]').text()).toContain('Something broke.');
    expect(button(page, 'Add to watchlist').attributes('disabled')).toBeUndefined();
  });

  it('deletes a movie after the user has confirmed, and goes back to the movies', async () => {
    const confirm = vi.spyOn(window, 'confirm').mockReturnValue(true);
    const { page, router } = await open(HOME_VIDEO);

    await button(page, 'Delete').trigger('click');
    expect(confirm).toHaveBeenCalledWith('Delete "Home Video"? This cannot be undone.');
    const request = backend.expectOne('/api/movies/m2');
    expect(request.method).toBe('DELETE');
    request.flush(null, { status: 204, statusText: 'No Content' });
    await flushPromises();

    expect(router.currentRoute.value.path).toBe('/movies');
  });

  it('does not delete a movie unless the user confirms', async () => {
    vi.spyOn(window, 'confirm').mockReturnValue(false);
    const { page, router } = await open(HOME_VIDEO);

    await button(page, 'Delete').trigger('click');

    expect(router.currentRoute.value.path).toBe('/movies/m2');
  });

  it('deletes a viewing after the user has confirmed, and reloads the movie', async () => {
    const confirm = vi.spyOn(window, 'confirm').mockReturnValue(true);
    const { page } = await open(ALIEN, ENTRIES);

    await page.findAll('tbody button')[1].trigger('click');
    expect(confirm).toHaveBeenCalledWith('Delete the diary entry from 2026-10-01?');
    const request = backend.expectOne('/api/diary-entries/e2');
    expect(request.method).toBe('DELETE');
    request.flush(null, { status: 204, statusText: 'No Content' });
    await flushPromises();

    // The watch count and the latest rating may have changed.
    await answer({ ...ALIEN, watchCount: 1 }, [ENTRIES[0]]);
    expect(page.findAll('tbody tr')).toHaveLength(1);
    expect(text(page)).toContain('Watched1×');
  });

  it('does not delete a viewing unless the user confirms', async () => {
    vi.spyOn(window, 'confirm').mockReturnValue(false);
    const { page } = await open(ALIEN, ENTRIES);

    await page.findAll('tbody button')[0].trigger('click');

    expect(page.findAll('tbody tr')).toHaveLength(2);
  });
});
