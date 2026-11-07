import { beforeEach, describe, expect, it, vi } from 'vitest';
import { useFakeBackend } from '../../test/fake-backend';
import { createHttp, type Http } from '../api/http';
import { addAuthInterceptors } from './auth.interceptor';

describe('auth interceptors', () => {
  const backend = useFakeBackend();
  let http: Http;
  let sessionEnded: ReturnType<typeof vi.fn<(returnUrl: string) => void>>;

  beforeEach(() => {
    sessionEnded = vi.fn();
    http = createHttp();
    addAuthInterceptors(http, { sessionEnded, currentUrl: () => '/diary?page=2' });
  });

  it('sends the CSRF header to the API only', async () => {
    const own = http.post('/api/movies', {});
    const other = http.get('https://example.com/poster.jpg');

    const apiRequest = backend.expectOne('/api/movies');
    expect(apiRequest.headers.get('X-CSRF')).toBe('1');
    apiRequest.flush({});
    const otherRequest = backend.expectOne('https://example.com/poster.jpg');
    expect(otherRequest.headers.has('X-CSRF')).toBe(false);
    otherRequest.flush({});
    await Promise.all([own, other]);
  });

  it('goes to the sign-in page, and comes back later, when the session has expired', async () => {
    const result = http.get('/api/diary-entries');
    const failed = expect(result).rejects.toMatchObject({ response: { status: 401 } });

    backend
      .expectOne('/api/diary-entries')
      .flush(null, { status: 401, statusText: 'Unauthorized' });
    await failed;

    expect(sessionEnded).toHaveBeenCalledExactlyOnceWith('/diary?page=2');
  });

  it('leaves 401 from the auth endpoints to the Auth service', async () => {
    const result = http.get('/api/auth/me');
    const failed = expect(result).rejects.toMatchObject({ response: { status: 401 } });

    backend.expectOne('/api/auth/me').flush(null, { status: 401, statusText: 'Unauthorized' });
    await failed;

    expect(sessionEnded).not.toHaveBeenCalled();
  });

  it('does not mind 401 from elsewhere than the API', async () => {
    const result = http.get('https://example.com/poster.jpg');
    const failed = expect(result).rejects.toMatchObject({ response: { status: 401 } });

    backend
      .expectOne('https://example.com/poster.jpg')
      .flush(null, { status: 401, statusText: 'Unauthorized' });
    await failed;

    expect(sessionEnded).not.toHaveBeenCalled();
  });

  it('does not mind other errors', async () => {
    const result = http.get('/api/movies');
    const failed = expect(result).rejects.toMatchObject({ response: { status: 500 } });

    backend.expectOne('/api/movies').flush(null, { status: 500, statusText: 'Error' });
    await failed;

    expect(sessionEnded).not.toHaveBeenCalled();
  });

  it('does not mind a server that cannot be reached', async () => {
    const result = http.get('/api/movies');
    const failed = expect(result).rejects.toMatchObject({ code: 'ERR_NETWORK' });

    backend.expectOne('/api/movies').networkError();
    await failed;

    expect(sessionEnded).not.toHaveBeenCalled();
  });
});
