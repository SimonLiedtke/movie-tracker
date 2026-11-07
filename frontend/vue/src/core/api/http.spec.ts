import { describe, expect, it } from 'vitest';
import { useFakeBackend } from '../../test/fake-backend';
import { HttpError, createHttp, type HttpInterceptor } from './http';
import { toSearchParams } from './http-params';

describe('toSearchParams', () => {
  it('leaves out empty values', () => {
    const params = toSearchParams({
      search: 'alien',
      genreId: undefined,
      year: null,
      from: '',
      page: 2,
    });
    expect(params.toString()).toBe('search=alien&page=2');
  });
});

describe('http', () => {
  const backend = useFakeBackend();

  it('sends query parameters', async () => {
    const result = createHttp().get('/api/movies', { search: 'alien', page: 2 });

    const request = backend.expectOne('/api/movies?search=alien&page=2');
    expect(request.method).toBe('GET');
    expect(request.body).toBeNull();
    request.flush({ items: [] });
    expect(await result).toEqual({ items: [] });
  });

  it('sends a body as JSON', async () => {
    const result = createHttp().put('/api/watchlist/42', { note: "Director's cut" });

    const request = backend.expectOne('/api/watchlist/42');
    expect(request.method).toBe('PUT');
    expect(request.headers.get('Content-Type')).toBe('application/json');
    expect(request.json).toEqual({ note: "Director's cut" });
    request.flush({});
    await result;
  });

  it('sends no body and no content type without a body', async () => {
    const result = createHttp().post('/api/movies/tmdb/348', null);

    const request = backend.expectOne('/api/movies/tmdb/348');
    expect(request.method).toBe('POST');
    expect(request.body).toBeNull();
    expect(request.headers.has('Content-Type')).toBe(false);
    request.flush({});
    await result;
  });

  it('sends a form as it is, so that the browser can set the multipart content type', async () => {
    const form = new FormData();
    form.append('file', new File(['zip'], 'export.zip'));
    const result = createHttp().post('/api/imports/letterboxd', form);

    const request = backend.expectOne('/api/imports/letterboxd');
    expect(request.body).toBe(form);
    expect(request.headers.has('Content-Type')).toBe(false);
    request.flush({});
    await result;
  });

  it('answers null when there is no body', async () => {
    const result = createHttp().delete('/api/movies/42');

    const request = backend.expectOne('/api/movies/42');
    expect(request.method).toBe('DELETE');
    request.flush(null, { status: 204, statusText: 'No Content' });
    expect(await result).toBeNull();
  });

  it('fails with the status and the problem details of the answer', async () => {
    const result = createHttp().get('/api/movies/42');
    const failure = expect(result).rejects.toMatchObject({
      status: 404,
      error: { title: 'Not Found' },
    });

    backend
      .expectOne('/api/movies/42')
      .flush({ title: 'Not Found' }, { status: 404, statusText: 'Not Found' });
    await failure;
    await expect(result).rejects.toBeInstanceOf(HttpError);
  });

  it('fails with status 0 when the server cannot be reached', async () => {
    const result = createHttp().get('/api/movies');
    const failure = expect(result).rejects.toMatchObject({ status: 0 });

    backend.expectOne('/api/movies').networkError();
    await failure;
  });

  it('lets interceptors change the request and see the failure, outermost first', async () => {
    const calls: string[] = [];
    const tag =
      (name: string): HttpInterceptor =>
      async (request, next) => {
        calls.push(`${name} in`);
        const headers = new Headers(request.headers);
        headers.set(`X-${name}`, '1');
        try {
          return await next({ ...request, headers });
        } finally {
          calls.push(`${name} out`);
        }
      };
    const http = createHttp();
    http.use(tag('A'));
    http.use(tag('B'));

    const result = http.get('/api/genres');
    const request = backend.expectOne('/api/genres');
    expect(request.headers.get('X-A')).toBe('1');
    expect(request.headers.get('X-B')).toBe('1');
    request.flush([]);
    await result;

    expect(calls).toEqual(['A in', 'B in', 'B out', 'A out']);
  });
});
