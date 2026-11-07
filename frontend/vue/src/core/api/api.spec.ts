import { describe, expect, it } from 'vitest';
import { useFakeBackend } from '../../test/fake-backend';
import { createApi } from './api';
import { createHttp } from './http';

describe('API clients', () => {
  const backend = useFakeBackend();
  const api = () => createApi(createHttp());

  it('lists movies without empty query parameters', async () => {
    const result = api().movies.list({
      search: 'alien',
      genreId: undefined,
      sort: '-releaseDate',
      page: 2,
    });

    const request = backend.expectOne((r) => r.url === '/api/movies');
    expect(request.params.toString()).toBe('search=alien&sort=-releaseDate&page=2');
    request.flush({ items: [], page: 2, pageSize: 20, totalCount: 0 });
    await result;
  });

  it('searches movies', async () => {
    const result = api().movies.search('the thing');

    const request = backend.expectOne((r) => r.url === '/api/movies/search');
    expect(request.params.get('query')).toBe('the thing');
    request.flush({ items: [], tmdbAvailable: true });
    await result;
  });

  it('imports a movie from TMDB', async () => {
    const result = api().movies.importFromTmdb(348);

    const request = backend.expectOne('/api/movies/tmdb/348');
    expect(request.method).toBe('POST');
    request.flush({});
    await result;
  });

  it('puts a movie on the watchlist with a note', async () => {
    const result = api().watchlist.put('42', "Director's cut");

    const request = backend.expectOne('/api/watchlist/42');
    expect(request.method).toBe('PUT');
    expect(request.json).toEqual({ note: "Director's cut" });
    request.flush({});
    await result;
  });

  it('uploads a Letterboxd export as a file in a multipart form', async () => {
    const file = new File(['zip'], 'export.zip', { type: 'application/zip' });
    const result = api().letterboxdImport.start(file);

    const request = backend.expectOne('/api/imports/letterboxd');
    expect(request.method).toBe('POST');
    const body = request.body as FormData;
    expect(body.get('file')).toBeInstanceOf(File);
    expect((body.get('file') as File).name).toBe('export.zip');
    request.flush({});
    await result;
  });

  it('asks for the progress of an import', async () => {
    const result = api().letterboxdImport.get('abc');

    const request = backend.expectOne('/api/imports/letterboxd/abc');
    expect(request.method).toBe('GET');
    request.flush({});
    await result;
  });

  it('asks for the genres once, since they do not change', async () => {
    const { genres } = api();
    const first = genres.list();
    const second = genres.list();

    backend.expectOne('/api/genres').flush([{ id: 28, name: 'Action' }]);
    expect(await first).toEqual([{ id: 28, name: 'Action' }]);
    expect(await second).toEqual([{ id: 28, name: 'Action' }]);
    // Later calls are answered from memory: no request (the backend verifies that after the test).
    expect(await genres.list()).toEqual([{ id: 28, name: 'Action' }]);
  });

  it('asks for the genres again after a failure', async () => {
    const { genres } = api();
    const failed = expect(genres.list()).rejects.toMatchObject({ response: { status: 503 } });
    backend.expectOne('/api/genres').flush(null, { status: 503, statusText: 'Unavailable' });
    await failed;

    const retried = genres.list();
    backend.expectOne('/api/genres').flush([]);
    expect(await retried).toEqual([]);
  });
});
