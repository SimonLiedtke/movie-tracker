import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { MoviesApi, WatchlistApi } from './api';

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

  it('puts a movie on the watchlist with a note', () => {
    TestBed.inject(WatchlistApi).put('42', "Director's cut").subscribe();

    const request = http.expectOne('/api/watchlist/42');
    expect(request.request.method).toBe('PUT');
    expect(request.request.body).toEqual({ note: "Director's cut" });
    request.flush({});
  });
});
