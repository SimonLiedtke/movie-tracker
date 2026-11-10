import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { LetterboxdImportApi, MoviesApi, WatchlistApi } from './api';

describe('API services', () => {
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('lists movies without empty query parameters', () => {
    TestBed.inject(MoviesApi)
      .list({ search: 'alien', genreId: undefined, sort: '-releaseDate', page: 2 })
      .subscribe();

    const request = http.expectOne((r) => r.url === '/api/movies');
    expect(request.request.params.toString()).toBe('search=alien&sort=-releaseDate&page=2');
    request.flush({ items: [], page: 2, pageSize: 20, totalCount: 0 });
  });

  it('searches movies', () => {
    TestBed.inject(MoviesApi).search('the thing').subscribe();

    const request = http.expectOne((r) => r.url === '/api/movies/search');
    expect(request.request.params.get('query')).toBe('the thing');
    request.flush({ items: [], tmdbAvailable: true });
  });

  it('imports a movie from TMDB', () => {
    TestBed.inject(MoviesApi).importFromTmdb(348).subscribe();

    const request = http.expectOne('/api/movies/tmdb/348');
    expect(request.request.method).toBe('POST');
    request.flush({});
  });

  it('lists the watchlist without filter', () => {
    TestBed.inject(WatchlistApi).list({ page: 2, pageSize: 20 }).subscribe();

    http.expectOne('/api/watchlist?page=2&pageSize=20').flush({});
  });

  it('lists the watchlist filtered by genres, repeating the parameter', () => {
    TestBed.inject(WatchlistApi)
      .list({ genreIds: [27, 878], genreMatch: 'all', page: 1, pageSize: 20 })
      .subscribe();

    http
      .expectOne('/api/watchlist?page=1&pageSize=20&genreId=27&genreId=878&genreMatch=all')
      .flush({});
  });

  it('lists the watchlist filtered by the release period', () => {
    TestBed.inject(WatchlistApi)
      .list({ releasedFrom: '1979-01-01', releasedTo: '1989-12-31', page: 1 })
      .subscribe();

    http.expectOne('/api/watchlist?page=1&releasedFrom=1979-01-01&releasedTo=1989-12-31').flush({});
  });

  it('lists the watchlist filtered by the period it was added in', () => {
    TestBed.inject(WatchlistApi)
      .list({ addedFrom: '2026-11-01', addedTo: '2026-11-05', page: 1 })
      .subscribe();

    http.expectOne('/api/watchlist?page=1&addedFrom=2026-11-01&addedTo=2026-11-05').flush({});
  });

  it('lists the watchlist filtered by the runtime', () => {
    TestBed.inject(WatchlistApi).list({ runtimeFrom: 23, runtimeTo: 148, page: 1 }).subscribe();

    http.expectOne('/api/watchlist?page=1&runtimeFrom=23&runtimeTo=148').flush({});
  });

  it('matches any genre by default, and sends no match mode without genres', () => {
    const api = TestBed.inject(WatchlistApi);
    api.list({ genreIds: [27] }).subscribe();
    api.list({ genreIds: [], genreMatch: 'all' }).subscribe();

    http.expectOne('/api/watchlist?genreId=27&genreMatch=any').flush({});
    http.expectOne('/api/watchlist').flush({});
  });

  it('puts a movie on the watchlist with a note', () => {
    TestBed.inject(WatchlistApi).put('42', "Director's cut").subscribe();

    const request = http.expectOne('/api/watchlist/42');
    expect(request.request.method).toBe('PUT');
    expect(request.request.body).toEqual({ note: "Director's cut" });
    request.flush({});
  });

  it('uploads a Letterboxd export as a file in a multipart form', () => {
    const file = new File(['zip'], 'export.zip', { type: 'application/zip' });
    TestBed.inject(LetterboxdImportApi).start(file).subscribe();

    const request = http.expectOne('/api/imports/letterboxd');
    expect(request.request.method).toBe('POST');
    const body = request.request.body as FormData;
    expect(body.get('file')).toBeInstanceOf(File);
    expect((body.get('file') as File).name).toBe('export.zip');
    request.flush({});
  });

  it('asks for the progress of an import', () => {
    TestBed.inject(LetterboxdImportApi).get('abc').subscribe();

    const request = http.expectOne('/api/imports/letterboxd/abc');
    expect(request.request.method).toBe('GET');
    request.flush({});
  });
});
