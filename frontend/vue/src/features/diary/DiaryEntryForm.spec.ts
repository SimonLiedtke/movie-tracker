import { flushPromises, type VueWrapper } from '@vue/test-utils';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { createApi } from '../../core/api/api';
import { createHttp } from '../../core/api/http';
import type { DiaryEntry, Movie } from '../../core/api/models';
import { diaryEntryFormProps } from '../../route-props';
import { todayIso } from '../../shared/dates';
import { useFakeBackend } from '../../test/fake-backend';
import { mountRoute } from '../../test/services';
import DiaryEntryForm from './DiaryEntryForm.vue';

const ALIEN: Movie = {
  id: 'm1',
  title: 'Alien',
  originalTitle: 'Alien',
  releaseDate: '1979-05-25',
  posterUrl: null,
  runtimeMinutes: 117,
  overview: null,
  genres: [],
  tmdbId: 348,
  latestRating: null,
  watchCount: 0,
  lastWatchedOn: null,
  onWatchlist: false,
  createdAt: '2026-10-01T10:00:00Z',
};

const ENTRY: DiaryEntry = {
  id: 'e1',
  movie: { id: 'm1', title: 'Alien', releaseDate: '1979-05-25', posterUrl: null },
  watchedOn: '2026-10-31',
  rating: 7,
  review: 'Scary.',
  isRewatch: true,
  liked: true,
  createdAt: '2026-10-31T20:00:00Z',
};

describe('DiaryEntryForm', () => {
  const backend = useFakeBackend();

  afterEach(() => vi.useRealTimers());

  const open = (url = '/diary/new') =>
    mountRoute(
      url,
      [
        { path: '/diary/new', component: DiaryEntryForm, props: diaryEntryFormProps },
        { path: '/diary/:id/edit', component: DiaryEntryForm, props: diaryEntryFormProps },
      ],
      { api: createApi(createHttp()) },
    );

  /** Opens the form for a movie: the URL names it, and the page asks for it. */
  async function openForMovie(movie: Movie = ALIEN) {
    const opened = await open(`/diary/new?movieId=${movie.id}`);
    backend.expectOne(`/api/movies/${movie.id}`).flush(movie);
    await flushPromises();
    return opened;
  }

  async function openForEntry(entry: DiaryEntry = ENTRY) {
    const opened = await open(`/diary/${entry.id}/edit`);
    backend.expectOne(`/api/diary-entries/${entry.id}`).flush(entry);
    await flushPromises();
    return opened;
  }

  const input = (page: VueWrapper, id: string) => page.get<HTMLInputElement>(`#${id}`);
  const errors = (page: VueWrapper) => page.findAll('.help.is-danger').map((help) => help.text());
  const selectedRating = (page: VueWrapper) =>
    page.get<HTMLSelectElement>('#rating').element.selectedOptions[0].text;
  /** Chooses the option at `index` of the rating select, like a user does. */
  async function chooseRating(page: VueWrapper, index: number) {
    const select = page.get<HTMLSelectElement>('#rating');
    select.element.selectedIndex = index;
    await select.trigger('change');
  }
  const submit = (page: VueWrapper) => page.get('form').trigger('submit');
  const checkboxes = (page: VueWrapper) =>
    page.findAll<HTMLInputElement>('input[type=checkbox]').map((box) => box.element.checked);

  it('starts with today and no movie', async () => {
    const { page } = await open();

    expect(page.get('h1').text()).toBe('Log a viewing');
    expect(input(page, 'watchedOn').element.value).toBe(todayIso());
    expect(input(page, 'watchedOn').attributes('max')).toBe(todayIso());
    expect(selectedRating(page)).toBe('No rating');
    expect(checkboxes(page)).toEqual([false, false]);
    expect(page.get('#movie').attributes('placeholder')).toBe('Search a movie by title');
    expect(page.get('a.button').attributes('href')).toBe('/diary');
    expect(errors(page)).toEqual([]);
  });

  it('offers ratings from half a star to five stars', async () => {
    const { page } = await open();

    const options = page.findAll('#rating option');
    expect(options).toHaveLength(11);
    expect(options[0].text()).toBe('No rating');
    expect(options[1].text()).toBe('½ (0.5)');
    expect(options[10].text()).toBe('★★★★★ (5)');
  });

  it('does not save without a movie, and says so', async () => {
    const { page } = await open();

    await submit(page);

    expect(errors(page)).toEqual(['Please pick a movie.']);
    expect(page.get('#movie').classes()).toContain('is-danger');
  });

  it('does not save without a date, and says so', async () => {
    const { page } = await openForMovie();
    await input(page, 'watchedOn').setValue('');
    expect(errors(page)).toEqual(['Please enter the date you watched the movie.']);

    await submit(page);

    expect(errors(page)).toEqual(['Please enter the date you watched the movie.']);
  });

  it('does not accept a review that is too long', async () => {
    const { page } = await openForMovie();
    expect(errors(page)).toEqual([]);

    await page.get('#review').setValue('x'.repeat(20001));
    expect(errors(page)).toEqual(['At most 20,000 characters.']);

    await submit(page);
  });

  it('preselects the movie of the URL', async () => {
    const { page } = await openForMovie();

    expect(input(page, 'movie').element.value).toBe('Alien (1979)');
    expect(page.get('a.button').attributes('href')).toBe('/movies/m1');
    expect(checkboxes(page)).toEqual([false, false]); // Not watched before.
  });

  it('suggests a rewatch for a movie that was watched before', async () => {
    const { page } = await openForMovie({ ...ALIEN, watchCount: 2 });

    expect(checkboxes(page)).toEqual([true, false]);
  });

  it('logs a viewing and shows the movie', async () => {
    const { page, router } = await openForMovie();
    await input(page, 'watchedOn').setValue('2026-11-01');
    await page.get('#rating').setValue(9);
    await page.get('#review').setValue('  Great, again.\n');
    await page.findAll('input[type=checkbox]')[1].setValue(true);

    await submit(page);
    expect(page.get('button[type=submit]').classes()).toContain('is-loading');
    const request = backend.expectOne('/api/diary-entries');
    expect(request.method).toBe('POST');
    expect(request.json).toEqual({
      movieId: 'm1',
      watchedOn: '2026-11-01',
      rating: 9,
      review: 'Great, again.',
      isRewatch: false,
      liked: true,
    });
    request.flush({ ...ENTRY, id: 'e9' }, { status: 201, statusText: 'Created' });
    await flushPromises();

    expect(router.currentRoute.value.fullPath).toBe('/movies/m1');
  });

  it('sends no rating and no review if there are none', async () => {
    const { page } = await openForMovie();

    await submit(page);

    const request = backend.expectOne('/api/diary-entries');
    expect(request.json).toEqual({
      movieId: 'm1',
      watchedOn: todayIso(),
      rating: null,
      review: null,
      isRewatch: false,
      liked: false,
    });
    request.flush(ENTRY);
    await flushPromises();
  });

  it('picks the movie to log from a search', async () => {
    const { page, router } = await open();
    vi.useFakeTimers({ toFake: ['setTimeout', 'clearTimeout'] });

    await page.get('#movie').setValue('alien');
    await vi.advanceTimersByTimeAsync(250);
    backend.expectOne('/api/movies/search?query=alien').flush({
      tmdbAvailable: true,
      items: [
        {
          movieId: null,
          tmdbId: 348,
          title: 'Alien',
          originalTitle: 'Alien',
          releaseDate: '1979-05-25',
          posterUrl: null,
        },
      ],
    });
    await flushPromises();
    await page.get('a.dropdown-item').trigger('click'); // TMDB movies are imported first.
    backend.expectOne('/api/movies/tmdb/348').flush(ALIEN);
    await flushPromises();
    expect(input(page, 'movie').element.value).toBe('Alien (1979)');
    expect(errors(page)).toEqual([]);

    await submit(page);
    const request = backend.expectOne('/api/diary-entries');
    expect(request.json).toMatchObject({ movieId: 'm1' });
    request.flush(ENTRY);
    await flushPromises();
    expect(router.currentRoute.value.path).toBe('/movies/m1');
  });

  it('lets the user change the movie', async () => {
    const { page } = await openForMovie();

    await page.get('.field.has-addons button').trigger('click');

    expect(input(page, 'movie').attributes('type')).toBe('search');
    expect(input(page, 'movie').element.value).toBe('');
    await submit(page);
    expect(errors(page)).toEqual(['Please pick a movie.']);
  });

  it('shows why a viewing could not be saved, and lets the user try again', async () => {
    const { page } = await openForMovie();

    await submit(page);
    backend
      .expectOne('/api/diary-entries')
      .flush({ detail: 'Something broke.' }, { status: 500, statusText: 'Error' });
    await flushPromises();

    expect(page.get('[role=alert]').text()).toContain('Something broke.');
    expect(page.get('button[type=submit]').classes()).not.toContain('is-loading');

    await submit(page);
    expect(page.find('[role=alert]').exists()).toBe(false);
    backend.expectOne('/api/diary-entries').flush(ENTRY);
    await flushPromises();
  });

  it('fills the form with the viewing to edit', async () => {
    const { page } = await openForEntry();

    expect(page.get('h1').text()).toBe('Edit diary entry');
    expect(input(page, 'movie').element.value).toBe('Alien (1979)');
    expect(input(page, 'watchedOn').element.value).toBe('2026-10-31');
    expect(selectedRating(page)).toBe('★★★½ (3.5)');
    expect(page.get<HTMLTextAreaElement>('#review').element.value).toBe('Scary.');
    expect(checkboxes(page)).toEqual([true, true]);
    expect(page.get('a.button').attributes('href')).toBe('/diary');
  });

  it('shows a progress bar while the viewing to edit is loaded', async () => {
    const { page } = await open('/diary/e1/edit');
    expect(page.find('progress').exists()).toBe(true);
    expect(page.find('form').exists()).toBe(false);

    backend.expectOne('/api/diary-entries/e1').flush(ENTRY);
    await flushPromises();
    expect(page.find('progress').exists()).toBe(false);
  });

  it('saves the changes of a viewing and shows the movie', async () => {
    const { page, router } = await openForEntry();
    await chooseRating(page, 0); // No rating.
    await page.get('#review').setValue('');

    await submit(page);
    const request = backend.expectOne('/api/diary-entries/e1');
    expect(request.method).toBe('PUT');
    expect(request.json).toEqual({
      movieId: 'm1',
      watchedOn: '2026-10-31',
      rating: null,
      review: null,
      isRewatch: true,
      liked: true,
    });
    request.flush(ENTRY);
    await flushPromises();

    expect(router.currentRoute.value.fullPath).toBe('/movies/m1');
  });

  it('shows why the viewing to edit cannot be loaded', async () => {
    const { page } = await open('/diary/e1/edit');

    backend
      .expectOne('/api/diary-entries/e1')
      .flush({ title: 'Not Found' }, { status: 404, statusText: 'Not Found' });
    await flushPromises();

    expect(page.get('[role=alert]').text()).toContain('Not Found');
    expect(page.find('progress').exists()).toBe(false);
  });

  it('shows why the preselected movie cannot be loaded', async () => {
    const { page } = await open('/diary/new?movieId=gone');

    backend
      .expectOne('/api/movies/gone')
      .flush({ title: 'Not Found' }, { status: 404, statusText: 'Not Found' });
    await flushPromises();

    expect(page.get('[role=alert]').text()).toContain('Not Found');
    expect(input(page, 'movie').element.value).toBe('');
  });
});
