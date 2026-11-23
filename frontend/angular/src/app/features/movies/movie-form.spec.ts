import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter, withComponentInputBinding } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { Movie } from '../../core/api/models';
import { MovieForm } from './movie-form';

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
const UPLOADED: Movie = { ...HOME_VIDEO, posterUrl: '/api/movies/m1/poster' };

const POSTER_REJECTED = 'Please choose a JPEG, PNG or WebP image of at most 5 MB.';
const POSTER_MAX_BYTES = 5 * 1024 * 1024;
const IMAGE = () => new File(['jpeg'], 'home.jpg', { type: 'image/jpeg' });

describe('MovieForm poster image', () => {
  let http: HttpTestingController;
  let harness: RouterTestingHarness;
  const revokeObjectURL = vi.fn();

  beforeEach(async () => {
    // The test DOM's File is not what Node's URL.createObjectURL expects.
    vi.spyOn(URL, 'createObjectURL').mockReturnValue('blob:preview');
    vi.spyOn(URL, 'revokeObjectURL').mockImplementation(revokeObjectURL);
    TestBed.configureTestingModule({
      providers: [
        provideRouter(
          [
            { path: 'movies/new', component: MovieForm },
            { path: 'movies/:id/edit', component: MovieForm },
            { path: 'movies/:id', children: [] },
            { path: 'diary/new', children: [] },
          ],
          withComponentInputBinding(),
        ),
        provideHttpClient(),
        provideHttpClientTesting(),
      ],
    });
    http = TestBed.inject(HttpTestingController);
    harness = await RouterTestingHarness.create();
  });

  afterEach(() => {
    http.verify();
    vi.restoreAllMocks();
    revokeObjectURL.mockClear();
  });

  const page = () => harness.routeNativeElement as HTMLElement;
  const text = () => page().textContent!.replace(/\s+/g, ' ');
  const url = () => TestBed.inject(Router).url;
  const input = (id: string) => page().querySelector<HTMLInputElement>(`#${id}`)!;
  const errors = () =>
    Array.from(page().querySelectorAll('.help.is-danger')).map((help) => help.textContent!.trim());
  const clearButton = () => page().querySelector<HTMLButtonElement>('button.is-light')!;
  const settle = async () => {
    harness.detectChanges();
    await harness.fixture.whenStable();
  };

  async function open(target = '/movies/new', movie?: Movie) {
    await harness.navigateByUrl(target, MovieForm);
    http.expectOne('/api/genres').flush([{ id: 18, name: 'Drama' }]);
    if (movie) {
      http.expectOne(`/api/movies/${movie.id}`).flush(movie);
    }
    await settle();
  }

  async function setValue(id: string, value: string) {
    const field = input(id);
    field.value = value;
    field.dispatchEvent(new Event('input'));
    await settle();
  }

  async function choosePoster(file: File) {
    const field = input('posterFile');
    Object.defineProperty(field, 'files', { value: [file], configurable: true });
    field.dispatchEvent(new Event('change'));
    await settle();
  }

  async function submit() {
    page().querySelector('form')!.dispatchEvent(new Event('submit'));
    await settle();
  }

  it('offers a file field for JPEG, PNG and WebP images', async () => {
    await open();

    expect(input('posterFile').accept).toBe('image/jpeg,image/png,image/webp');
    expect(text()).toContain('No file chosen');
    expect(errors()).toEqual([]);
  });

  it('uploads the chosen image after adding the movie, and then shows the movie', async () => {
    await open();
    await setValue('title', 'Home Video');
    const image = IMAGE();
    await choosePoster(image);
    expect(text()).toContain('home.jpg');

    await submit();
    const create = http.expectOne('/api/movies');
    expect(create.request.body).toMatchObject({ title: 'Home Video', posterUrl: null });
    create.flush({ ...HOME_VIDEO, id: 'm9', posterUrl: null });
    await settle();
    const upload = http.expectOne('/api/movies/m9/poster');
    expect(upload.request.method).toBe('PUT');
    expect((upload.request.body as FormData).get('file')).toBe(image);
    upload.flush({ ...HOME_VIDEO, id: 'm9', posterUrl: '/api/movies/m9/poster' });
    await settle();

    expect(url()).toBe('/movies/m9');
  });

  it('uploads the image before going on to log a viewing', async () => {
    await open('/movies/new?next=diary');
    await setValue('title', 'Home Video');
    await choosePoster(IMAGE());

    await submit();
    http.expectOne('/api/movies').flush({ ...HOME_VIDEO, id: 'm9' });
    await settle();
    expect(url()).toBe('/movies/new?next=diary');
    http.expectOne('/api/movies/m9/poster').flush({ ...HOME_VIDEO, id: 'm9' });
    await settle();

    expect(url()).toBe('/diary/new?movieId=m9');
  });

  it('does not upload anything without a chosen image', async () => {
    await open();
    await setValue('title', 'Home Video');

    await submit();
    http.expectOne('/api/movies').flush({ ...HOME_VIDEO, id: 'm9' });
    await settle();

    http.verify();
  });

  it('does not save a file that is not an image the server accepts', async () => {
    await open();
    await setValue('title', 'Home Video');

    await choosePoster(new File(['gif'], 'home.gif', { type: 'image/gif' }));
    await submit();
    expect(errors()).toEqual([POSTER_REJECTED]);
    http.verify();

    const tooBig = new File([new Uint8Array(POSTER_MAX_BYTES + 1)], 'big.png', {
      type: 'image/png',
    });
    await choosePoster(tooBig);
    expect(errors()).toEqual([POSTER_REJECTED]);

    await choosePoster(IMAGE());
    expect(errors()).toEqual([]);
  });

  it('previews the chosen image, and lets go of the preview when it is not needed', async () => {
    await open();
    expect(page().querySelector('img')).toBeNull();

    await choosePoster(IMAGE());
    expect(page().querySelector('img')!.getAttribute('src')).toBe('blob:preview');
    expect(revokeObjectURL).not.toHaveBeenCalled();

    clearButton().click();
    await settle();
    expect(page().querySelector('img')).toBeNull();
    expect(revokeObjectURL).toHaveBeenCalledWith('blob:preview');
  });

  it('forgets the chosen image on request', async () => {
    await open();
    await setValue('title', 'Home Video');
    await choosePoster(IMAGE());

    clearButton().click();
    await settle();
    expect(text()).toContain('No file chosen');
    await submit();

    http.expectOne('/api/movies').flush({ ...HOME_VIDEO, id: 'm9' });
    await settle();
    http.verify();
  });

  it('shows a failed upload and saves the movie that was added only once', async () => {
    await open();
    await setValue('title', 'Home Video');
    await choosePoster(IMAGE());

    await submit();
    http.expectOne('/api/movies').flush({ ...HOME_VIDEO, id: 'm9' });
    await settle();
    http
      .expectOne('/api/movies/m9/poster')
      .flush(
        { title: 'One or more validation errors occurred.', errors: { file: ['Not an image.'] } },
        { status: 400, statusText: 'Bad Request' },
      );
    await settle();
    expect(text()).toContain('Not an image.');
    expect(url()).toBe('/movies/new');

    // Saving again changes the movie that exists now, instead of adding it a second time.
    await submit();
    const update = http.expectOne('/api/movies/m9');
    expect(update.request.method).toBe('PUT');
    update.flush({ ...HOME_VIDEO, id: 'm9' });
    await settle();
    http.expectOne('/api/movies/m9/poster').flush({ ...HOME_VIDEO, id: 'm9' });
    await settle();

    expect(url()).toBe('/movies/m9');
  });

  it('replaces the poster of a movie by uploading another image', async () => {
    await open('/movies/m1/edit', HOME_VIDEO);
    expect(input('posterUrl').value).toBe('https://image/home.jpg');
    await choosePoster(IMAGE());

    await submit();
    const update = http.expectOne('/api/movies/m1');
    expect(update.request.method).toBe('PUT');
    update.flush(HOME_VIDEO);
    await settle();
    const upload = http.expectOne('/api/movies/m1/poster');
    expect(upload.request.method).toBe('PUT');
    upload.flush(UPLOADED);
    await settle();

    expect(url()).toBe('/movies/m1');
  });

  it('shows that the movie has an uploaded poster, not its path as URL', async () => {
    await open('/movies/m1/edit', UPLOADED);

    expect(input('posterUrl').value).toBe('');
    expect(text()).toContain('Uploaded image');
    expect(page().querySelector('img')!.getAttribute('src')).toBe('/api/movies/m1/poster');
  });

  it('keeps the uploaded poster when other things are changed, by sending its URL back', async () => {
    await open('/movies/m1/edit', UPLOADED);
    await setValue('title', 'Home Video 2');

    await submit();

    const update = http.expectOne('/api/movies/m1');
    expect(update.request.body).toMatchObject({
      title: 'Home Video 2',
      posterUrl: '/api/movies/m1/poster',
    });
    update.flush(UPLOADED);
    await settle();
    http.verify();
  });

  it('removes the uploaded poster on request', async () => {
    await open('/movies/m1/edit', UPLOADED);

    clearButton().click();
    await settle();
    expect(text()).toContain('No file chosen');
    expect(page().querySelector('img')).toBeNull();
    await submit();

    const update = http.expectOne('/api/movies/m1');
    expect(update.request.body).toMatchObject({ posterUrl: null });
    update.flush({ ...HOME_VIDEO, posterUrl: null });
    await settle();
  });

  it('replaces the uploaded poster by a poster URL', async () => {
    await open('/movies/m1/edit', UPLOADED);
    await setValue('posterUrl', 'https://image/other.jpg');

    await submit();

    const update = http.expectOne('/api/movies/m1');
    expect(update.request.body).toMatchObject({ posterUrl: 'https://image/other.jpg' });
    update.flush({ ...HOME_VIDEO, posterUrl: 'https://image/other.jpg' });
    await settle();
    http.verify();
  });

  it('keeps the uploaded poster and replaces it by the chosen image', async () => {
    await open('/movies/m1/edit', UPLOADED);
    await choosePoster(IMAGE());

    await submit();

    const update = http.expectOne('/api/movies/m1');
    expect(update.request.body).toMatchObject({ posterUrl: '/api/movies/m1/poster' });
    update.flush(UPLOADED);
    await settle();
    http.expectOne('/api/movies/m1/poster').flush(UPLOADED);
    await settle();
  });
});
