import { beforeEach, describe, expect, it, vi, type Mock } from 'vitest';
import type { User } from '../api/models';
import { createHttp } from '../api/http';
import { useFakeBackend } from '../../test/fake-backend';
import { createAuth, isLocalPath, type Auth, type AuthOptions } from './auth';

const ALICE: User = { id: '1', email: 'alice@example.com', name: 'Alice', pictureUrl: null };

describe('Auth', () => {
  const backend = useFakeBackend();
  let auth: Auth;
  let navigate: Mock<AuthOptions['navigate']>;

  beforeEach(() => {
    navigate = vi.fn<AuthOptions['navigate']>();
    auth = createAuth({ http: createHttp(), navigate });
  });

  it('loads the signed-in user', async () => {
    const loading = auth.load();
    backend.expectOne('/api/auth/me').flush(ALICE);
    await loading;

    expect(auth.user.value).toEqual(ALICE);
    expect(auth.signedIn.value).toBe(true);
  });

  it('is signed out when the API answers 401', async () => {
    const loading = auth.load();
    backend.expectOne('/api/auth/me').flush(null, { status: 401, statusText: 'Unauthorized' });
    await loading;

    expect(auth.signedIn.value).toBe(false);
  });

  it('is signed out, and says why, when the user cannot be loaded', async () => {
    const error = vi.spyOn(console, 'error').mockImplementation(() => undefined);
    const loading = auth.load();
    backend.expectOne('/api/auth/me').networkError();
    await loading;

    expect(auth.signedIn.value).toBe(false);
    expect(error).toHaveBeenCalledOnce();
    error.mockRestore();
  });

  it('builds the sign-in URL with the page to return to', () => {
    expect(auth.loginUrl('/diary?page=2')).toBe('/api/auth/login?returnUrl=%2Fdiary%3Fpage%3D2');
    expect(auth.loginUrl()).toBe('/api/auth/login?returnUrl=%2Fmovies');
  });

  it('signs out and goes to the sign-in page', async () => {
    const loading = auth.load();
    backend.expectOne('/api/auth/me').flush(ALICE);
    await loading;

    const logout = auth.logout();
    const request = backend.expectOne('/api/auth/logout');
    expect(request.method).toBe('POST');
    request.flush(null, { status: 204, statusText: 'No Content' });
    await logout;

    expect(auth.signedIn.value).toBe(false);
    expect(navigate).toHaveBeenCalledWith({ path: '/', query: {} });
  });

  it('goes to the sign-in page even if signing out failed', async () => {
    const logout = auth.logout();
    const failed = expect(logout).rejects.toMatchObject({ status: 500 });
    backend.expectOne('/api/auth/logout').flush(null, { status: 500, statusText: 'Error' });
    await failed;

    expect(navigate).toHaveBeenCalledWith({ path: '/', query: {} });
  });

  it('remembers the page when the session has ended', () => {
    auth.sessionEnded('/diary?page=2');

    expect(navigate).toHaveBeenCalledWith({ path: '/', query: { returnUrl: '/diary?page=2' } });
  });
});

describe('isLocalPath', () => {
  it('accepts paths within the app', () => {
    expect(isLocalPath('/')).toBe(true);
    expect(isLocalPath('/watchlist')).toBe(true);
    expect(isLocalPath('/diary?page=2')).toBe(true);
  });

  it('rejects everything else', () => {
    for (const url of [
      undefined,
      null,
      '',
      'diary',
      'https://evil.example.com',
      '//evil.example.com',
      '/\\evil.example.com',
    ]) {
      expect(isLocalPath(url)).toBe(false);
    }
  });
});
