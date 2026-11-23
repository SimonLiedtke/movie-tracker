import { flushPromises, type VueWrapper } from '@vue/test-utils';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
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

const POSTER_REJECTED = 'Please choose a JPEG, PNG or WebP image of at most 5 MB.';
const POSTER_MAX_BYTES = 5 * 1024 * 1024;

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

  /** Chooses an image like the user does with the file dialog. */
  async function choosePoster(page: VueWrapper, file: File) {
    const field = page.get<HTMLInputElement>('input[type=file]');
    Object.defineProperty(field.element, 'files', { value: [file], configurable: true });
    await field.trigger('change');
  }

  const IMAGE = () => new File(['jpeg'], 'home.jpg', { type: 'image/jpeg' });
  const UPLOADED: Movie = { ...HOME_VIDEO, posterUrl: '/api/movies/m1/poster' };

  async function openForEditWith(movie: Movie) {
    const opening = open('/movies/m1/edit');
    const request = await backend.expectOneEventually('/api/movies/m1');
    request.flush(movie);
    const result = await opening;
    await flushPromises();
    return result;
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
  describe('poster image', () => {
    // jsdom's File is not what Node's URL.createObjectURL expects.
    const revokeObjectURL = vi.fn();
    beforeEach(() => {
      vi.spyOn(URL, 'createObjectURL').mockReturnValue('blob:preview');
      vi.spyOn(URL, 'revokeObjectURL').mockImplementation(revokeObjectURL);
    });
    afterEach(() => {
      vi.restoreAllMocks();
      revokeObjectURL.mockClear();
    });

    it('offers a file field for JPEG, PNG and WebP images', async () => {
      const { page } = await open();

      const field = page.get<HTMLInputElement>('input[type=file]');
      expect(field.attributes('accept')).toBe('image/jpeg,image/png,image/webp');
      expect(page.text()).toContain('No file chosen');
      expect(errors(page)).toEqual([]);
    });

    it('uploads the chosen image after adding the movie, and then shows the movie', async () => {
      const { page, router } = await open();
      await input(page, 'title').setValue('Home Video');
      const image = IMAGE();
      await choosePoster(page, image);
      expect(page.text()).toContain('home.jpg');

      await submit(page);
      const create = backend.expectOne('/api/movies');
      expect(create.json).toMatchObject({ title: 'Home Video', posterUrl: null });
      create.flush({ ...HOME_VIDEO, id: 'm9', posterUrl: null }, { status: 201 });
      await flushPromises();
      const upload = backend.expectOne('/api/movies/m9/poster');
      expect(upload.method).toBe('PUT');
      expect((upload.body as FormData).get('file')).toBe(image);
      upload.flush({ ...HOME_VIDEO, id: 'm9', posterUrl: '/api/movies/m9/poster' });
      await flushPromises();

      expect(router.currentRoute.value.fullPath).toBe('/movies/m9');
    });

    it('uploads the image before going on to log a viewing', async () => {
      const { page, router } = await open('/movies/new?next=diary');
      await input(page, 'title').setValue('Home Video');
      await choosePoster(page, IMAGE());

      await submit(page);
      backend.expectOne('/api/movies').flush({ ...HOME_VIDEO, id: 'm9' });
      await flushPromises();
      expect(router.currentRoute.value.fullPath).toBe('/movies/new?next=diary');
      backend.expectOne('/api/movies/m9/poster').flush({ ...HOME_VIDEO, id: 'm9' });
      await flushPromises();

      expect(router.currentRoute.value.fullPath).toBe('/diary/new?movieId=m9');
    });

    it('does not upload anything without a chosen image', async () => {
      const { page } = await open();
      await input(page, 'title').setValue('Home Video');

      await submit(page);
      backend.expectOne('/api/movies').flush({ ...HOME_VIDEO, id: 'm9' });
      await flushPromises();

      backend.verify();
    });

    it('does not save a file that is not an image the server accepts', async () => {
      const { page } = await open();
      await input(page, 'title').setValue('Home Video');

      await choosePoster(page, new File(['gif'], 'home.gif', { type: 'image/gif' }));
      await submit(page);
      expect(errors(page)).toEqual([POSTER_REJECTED]);
      backend.verify();

      const tooBig = new File([new Uint8Array(POSTER_MAX_BYTES + 1)], 'big.png', {
        type: 'image/png',
      });
      await choosePoster(page, tooBig);
      expect(errors(page)).toEqual([POSTER_REJECTED]);

      await choosePoster(page, IMAGE());
      expect(errors(page)).toEqual([]);
    });

    it('previews the chosen image, and lets go of the preview when it is not needed', async () => {
      const { page } = await open();
      expect(page.find('img').exists()).toBe(false);

      await choosePoster(page, IMAGE());
      expect(page.get('img').attributes('src')).toBe('blob:preview');
      expect(revokeObjectURL).not.toHaveBeenCalled();

      await page.get('button.is-light').trigger('click');
      expect(page.find('img').exists()).toBe(false);
      expect(revokeObjectURL).toHaveBeenCalledWith('blob:preview');
    });

    it('forgets the chosen image on request', async () => {
      const { page } = await open();
      await input(page, 'title').setValue('Home Video');
      await choosePoster(page, IMAGE());

      await page.get('button.is-light').trigger('click');
      expect(page.text()).toContain('No file chosen');
      await submit(page);

      backend.expectOne('/api/movies').flush({ ...HOME_VIDEO, id: 'm9' });
      await flushPromises();
      backend.verify();
    });

    it('shows a failed upload and saves the movie that was added only once', async () => {
      const { page, router } = await open();
      await input(page, 'title').setValue('Home Video');
      await choosePoster(page, IMAGE());

      await submit(page);
      backend.expectOne('/api/movies').flush({ ...HOME_VIDEO, id: 'm9' }, { status: 201 });
      await flushPromises();
      backend
        .expectOne('/api/movies/m9/poster')
        .flush(
          { title: 'One or more validation errors occurred.', errors: { file: ['Not an image.'] } },
          { status: 400, statusText: 'Bad Request' },
        );
      await flushPromises();
      expect(page.get('[role=alert]').text()).toContain('Not an image.');
      expect(router.currentRoute.value.fullPath).toBe('/movies/new');

      // Saving again changes the movie that exists now, instead of adding it a second time.
      await submit(page);
      const update = backend.expectOne('/api/movies/m9');
      expect(update.method).toBe('PUT');
      update.flush({ ...HOME_VIDEO, id: 'm9' });
      await flushPromises();
      backend.expectOne('/api/movies/m9/poster').flush({ ...HOME_VIDEO, id: 'm9' });
      await flushPromises();

      expect(router.currentRoute.value.fullPath).toBe('/movies/m9');
    });

    it('replaces the poster of a movie by uploading another image', async () => {
      const { page, router } = await openForEdit();
      expect(input(page, 'posterUrl').element.value).toBe('https://image/home.jpg');
      await choosePoster(page, IMAGE());

      await submit(page);
      const update = backend.expectOne('/api/movies/m1');
      expect(update.method).toBe('PUT');
      update.flush(HOME_VIDEO);
      await flushPromises();
      const upload = backend.expectOne('/api/movies/m1/poster');
      expect(upload.method).toBe('PUT');
      upload.flush(UPLOADED);
      await flushPromises();

      expect(router.currentRoute.value.fullPath).toBe('/movies/m1');
    });

    it('shows that the movie has an uploaded poster, not its path as URL', async () => {
      const { page } = await openForEditWith(UPLOADED);

      expect(input(page, 'posterUrl').element.value).toBe('');
      expect(page.text()).toContain('Uploaded image');
      expect(page.get('img').attributes('src')).toBe('/api/movies/m1/poster');
    });

    it('keeps the uploaded poster when other things are changed, by sending its URL back', async () => {
      const { page } = await openForEditWith(UPLOADED);
      await input(page, 'title').setValue('Home Video 2');

      await submit(page);

      const update = backend.expectOne('/api/movies/m1');
      expect(update.json).toMatchObject({
        title: 'Home Video 2',
        posterUrl: '/api/movies/m1/poster',
      });
      update.flush(UPLOADED);
      await flushPromises();
      backend.verify();
    });

    it('removes the uploaded poster on request', async () => {
      const { page } = await openForEditWith(UPLOADED);

      await page.get('button.is-light').trigger('click');
      expect(page.text()).toContain('No file chosen');
      expect(page.find('img').exists()).toBe(false);
      await submit(page);

      const update = backend.expectOne('/api/movies/m1');
      expect(update.json).toMatchObject({ posterUrl: null });
      update.flush({ ...HOME_VIDEO, posterUrl: null });
      await flushPromises();
    });

    it('replaces the uploaded poster by a poster URL', async () => {
      const { page } = await openForEditWith(UPLOADED);
      await input(page, 'posterUrl').setValue('https://image/other.jpg');

      await submit(page);

      const update = backend.expectOne('/api/movies/m1');
      expect(update.json).toMatchObject({ posterUrl: 'https://image/other.jpg' });
      update.flush({ ...HOME_VIDEO, posterUrl: 'https://image/other.jpg' });
      await flushPromises();
      backend.verify();
    });

    it('keeps the uploaded poster and replaces it by the chosen image', async () => {
      const { page } = await openForEditWith(UPLOADED);
      await choosePoster(page, IMAGE());

      await submit(page);

      const update = backend.expectOne('/api/movies/m1');
      expect(update.json).toMatchObject({ posterUrl: '/api/movies/m1/poster' });
      update.flush(UPLOADED);
      await flushPromises();
      backend.expectOne('/api/movies/m1/poster').flush(UPLOADED);
      await flushPromises();
    });
  });
});
