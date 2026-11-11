import { flushPromises, type VueWrapper } from '@vue/test-utils';
import { describe, expect, it } from 'vitest';
import { createApi } from '../../core/api/api';
import { createHttp } from '../../core/api/http';
import type { Genre, Movie } from '../../core/api/models';
import { movieFormProps } from '../../route-props';
import { useFakeBackend } from '../../test/fake-backend';
import { mountRoute } from '../../test/services';
import MovieForm from './MovieForm.vue';

const GENRES: Genre[] = [
  { id: 18, name: 'Drama' },
  { id: 27, name: 'Horror' },
];

const HOME_VIDEO: Movie = {
  id: 'm1',
  title: 'Home Video',
  originalTitle: 'Heimvideo',
  releaseDate: '2019-03-01',
  posterUrl: 'https://image/home.jpg',
  runtimeMinutes: 90,
  overview: 'Our holidays.',
  genres: [{ id: 18, name: 'Drama' }],
  tmdbId: null,
  latestRating: null,
  watchCount: 0,
  lastWatchedOn: null,
  onWatchlist: false,
  createdAt: '2026-10-01T10:00:00Z',
};

describe('MovieForm', () => {
  const backend = useFakeBackend();

  async function open(url = '/movies/new') {
    const { page, router } = await mountRoute(
      url,
      [
        { path: '/movies/new', component: MovieForm, props: movieFormProps },
        { path: '/movies/:id/edit', component: MovieForm, props: movieFormProps },
      ],
      { api: createApi(createHttp()) },
    );
    backend.expectOne('/api/genres').flush(GENRES);
    await flushPromises();
    return { page, router };
  }

  async function openForEdit() {
    const opening = open('/movies/m1/edit');
    // The movie is requested while the page is shown; answer it as soon as it is.
    const request = await backend.expectOneEventually('/api/movies/m1');
    request.flush(HOME_VIDEO);
    const result = await opening;
    await flushPromises();
    return result;
  }

  const input = (page: VueWrapper, id: string) => page.get<HTMLInputElement>(`#${id}`);
  const errors = (page: VueWrapper) => page.findAll('.help.is-danger').map((help) => help.text());
  const checked = (page: VueWrapper) =>
    page.findAll<HTMLInputElement>('input[type=checkbox]').map((box) => box.element.checked);
  const submit = (page: VueWrapper) => page.get('form').trigger('submit');

  async function fillIn(page: VueWrapper) {
    await input(page, 'title').setValue('  Home Video ');
    await input(page, 'originalTitle').setValue('Heimvideo');
    await input(page, 'releaseDate').setValue('2019-03-01');
    await input(page, 'runtimeMinutes').setValue('90');
    await input(page, 'posterUrl').setValue('https://image/home.jpg');
    await page.get('#overview').setValue('Our holidays.');
    await page.findAll('input[type=checkbox]')[1].setValue(true);
  }

  const REQUEST = {
    title: 'Home Video',
    originalTitle: 'Heimvideo',
    releaseDate: '2019-03-01',
    runtimeMinutes: 90,
    overview: 'Our holidays.',
    posterUrl: 'https://image/home.jpg',
    genreIds: [27],
  };

  it('starts with an empty form for a new movie, and offers the genres', async () => {
    const { page } = await open();

    expect(page.get('h1').text()).toBe('Add a movie manually');
    expect(page.get('.subtitle').text()).toContain('Only you can see the movies you add here');
    expect(input(page, 'title').element.value).toBe('');
    expect(checked(page)).toEqual([false, false]);
    expect(page.findAll('.checkbox').map((label) => label.text())).toEqual(['Drama', 'Horror']);
    expect(errors(page)).toEqual([]);
  });

  it('starts with the title the user searched for', async () => {
    const { page } = await open('/movies/new?title=Home%20Vid');

    expect(input(page, 'title').element.value).toBe('Home Vid');
    expect(errors(page)).toEqual([]);
  });

  it('does not save an incomplete movie, and shows what is missing', async () => {
    const { page } = await open();

    await submit(page);

    expect(errors(page)).toEqual(['Please enter a title (at most 300 characters).']);
    expect(input(page, 'title').classes()).toContain('is-danger');
  });

  it('shows what is wrong with a field once the user has been there', async () => {
    const { page } = await open();
    expect(errors(page)).toEqual([]);

    await input(page, 'title').trigger('blur');
    expect(errors(page)).toEqual(['Please enter a title (at most 300 characters).']);

    await input(page, 'title').setValue('x'.repeat(301));
    expect(errors(page)).toEqual(['Please enter a title (at most 300 characters).']);
    await input(page, 'title').setValue('x'.repeat(300));
    expect(errors(page)).toEqual([]);

    await input(page, 'originalTitle').setValue('x'.repeat(301));
    await input(page, 'runtimeMinutes').setValue('0');
    await input(page, 'posterUrl').setValue('ftp://image/home.jpg');
    await page.get('#overview').setValue('x'.repeat(5001));
    expect(errors(page)).toEqual([
      'At most 300 characters.',
      'Between 1 and 1500 minutes.',
      'Please enter a valid http(s) URL.',
      'At most 5000 characters.',
    ]);

    await input(page, 'originalTitle').setValue('');
    await input(page, 'runtimeMinutes').setValue('1500');
    await input(page, 'posterUrl').setValue('https://image/home.jpg');
    await page.get('#overview').setValue('');
    expect(errors(page)).toEqual([]);
  });

  it('does not mind empty runtime and poster', async () => {
    const { page } = await open();
    await input(page, 'runtimeMinutes').setValue('90');
    await input(page, 'runtimeMinutes').setValue('');
    await input(page, 'posterUrl').setValue('https://image/home.jpg');
    await input(page, 'posterUrl').setValue('');

    expect(errors(page)).toEqual([]);
  });

  it('adds the movie and shows it', async () => {
    const { page, router } = await open();
    await fillIn(page);

    await submit(page);
    expect(page.get('button[type=submit]').classes()).toContain('is-loading');
    const request = backend.expectOne('/api/movies');
    expect(request.method).toBe('POST');
    expect(request.json).toEqual(REQUEST);
    request.flush({ ...HOME_VIDEO, id: 'm9' }, { status: 201, statusText: 'Created' });
    await flushPromises();

    expect(router.currentRoute.value.fullPath).toBe('/movies/m9');
  });

  it('sends what is not filled in as null', async () => {
    const { page } = await open();
    await input(page, 'title').setValue('Home Video');

    await submit(page);

    const request = backend.expectOne('/api/movies');
    expect(request.json).toEqual({
      title: 'Home Video',
      originalTitle: null,
      releaseDate: null,
      runtimeMinutes: null,
      overview: null,
      posterUrl: null,
      genreIds: [],
    });
    request.flush({ ...HOME_VIDEO, id: 'm9' });
    await flushPromises();
  });

  it('goes on to log a viewing of the new movie, if that is where the user came from', async () => {
    const { page, router } = await open('/movies/new?title=Home&next=diary');
    await input(page, 'title').setValue('Home Video');

    await submit(page);
    backend.expectOne('/api/movies').flush({ ...HOME_VIDEO, id: 'm9' });
    await flushPromises();

    expect(router.currentRoute.value.fullPath).toBe('/diary/new?movieId=m9');
  });

  it('puts the new movie on the watchlist, if that is where the user came from', async () => {
    const { page, router } = await open('/movies/new?next=watchlist');
    await input(page, 'title').setValue('Home Video');

    await submit(page);
    backend.expectOne('/api/movies').flush({ ...HOME_VIDEO, id: 'm9' });
    await flushPromises();
    const put = backend.expectOne('/api/watchlist/m9');
    expect(put.method).toBe('PUT');
    expect(put.json).toEqual({ note: null, tags: [] });
    put.flush({});
    await flushPromises();

    expect(router.currentRoute.value.fullPath).toBe('/watchlist');
  });

  it('goes back to where the user came from when they cancel', async () => {
    const cancelTo = async (url: string) => {
      const { page } = await open(url);
      return page.get('a.button').attributes('href');
    };

    expect(await cancelTo('/movies/new')).toBe('/movies');
    expect(await cancelTo('/movies/new?next=diary')).toBe('/diary/new');
    expect(await cancelTo('/movies/new?next=watchlist')).toBe('/watchlist');
    expect(await cancelTo('/movies/new?next=elsewhere')).toBe('/movies');
  });

  it('shows what the server does not accept, and lets the user try again', async () => {
    const { page } = await open();
    await input(page, 'title').setValue('Home Video');

    await submit(page);
    backend.expectOne('/api/movies').flush(
      {
        title: 'One or more validation errors occurred.',
        errors: { genreIds: ['Unknown genre IDs: 99999.'] },
      },
      { status: 400, statusText: 'Bad Request' },
    );
    await flushPromises();

    expect(page.get('[role=alert]').text()).toContain('One or more validation errors occurred.');
    expect(page.get('[role=alert]').text()).toContain('Unknown genre IDs: 99999.');
    expect(page.get('button[type=submit]').classes()).not.toContain('is-loading');
    expect(input(page, 'title').element.value).toBe('Home Video');

    await submit(page);
    backend.expectOne('/api/movies').flush({ ...HOME_VIDEO, id: 'm9' });
    await flushPromises();
  });

  it('fills the form with the movie to edit', async () => {
    const { page } = await openForEdit();

    expect(page.get('h1').text()).toBe('Edit movie');
    expect(page.find('.subtitle').exists()).toBe(false);
    expect(input(page, 'title').element.value).toBe('Home Video');
    expect(input(page, 'originalTitle').element.value).toBe('Heimvideo');
    expect(input(page, 'releaseDate').element.value).toBe('2019-03-01');
    expect(input(page, 'runtimeMinutes').element.value).toBe('90');
    expect(input(page, 'posterUrl').element.value).toBe('https://image/home.jpg');
    expect(page.get<HTMLTextAreaElement>('#overview').element.value).toBe('Our holidays.');
    expect(checked(page)).toEqual([true, false]);
    expect(errors(page)).toEqual([]);
    expect(page.get('a.button').attributes('href')).toBe('/movies/m1');
  });

  it('shows a progress bar while the movie to edit is loaded', async () => {
    const opening = open('/movies/m1/edit');
    const request = await backend.expectOneEventually('/api/movies/m1');
    const { page } = await opening;
    expect(page.find('progress').exists()).toBe(true);
    expect(page.find('form').exists()).toBe(false);

    request.flush(HOME_VIDEO);
    await flushPromises();
    expect(page.find('progress').exists()).toBe(false);
    expect(page.find('form').exists()).toBe(true);
  });

  it('saves the changes of a movie and shows it', async () => {
    const { page, router } = await openForEdit();
    await input(page, 'title').setValue('Home Video 2');
    await page.findAll('input[type=checkbox]')[0].setValue(false);
    await page.findAll('input[type=checkbox]')[1].setValue(true);

    await submit(page);
    const request = backend.expectOne('/api/movies/m1');
    expect(request.method).toBe('PUT');
    expect(request.json).toMatchObject({ title: 'Home Video 2', genreIds: [27] });
    request.flush({ ...HOME_VIDEO, title: 'Home Video 2' });
    await flushPromises();

    expect(router.currentRoute.value.fullPath).toBe('/movies/m1');
  });

  it('shows why the movie to edit cannot be loaded', async () => {
    const opening = open('/movies/m1/edit');
    const request = await backend.expectOneEventually('/api/movies/m1');
    request.flush({ title: 'Not Found' }, { status: 404, statusText: 'Not Found' });
    const { page } = await opening;
    await flushPromises();

    expect(page.get('[role=alert]').text()).toContain('Not Found');
    expect(page.find('progress').exists()).toBe(false);
  });
});
