import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { User } from '../api/models';
import { Auth } from './auth';

const ALICE: User = { id: '1', email: 'alice@example.com', name: 'Alice', pictureUrl: null };

describe('Auth', () => {
  let auth: Auth;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    });
    auth = TestBed.inject(Auth);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('loads the signed-in user', async () => {
    const loading = auth.load();
    http.expectOne('/api/auth/me').flush(ALICE);
    await loading;

    expect(auth.user()).toEqual(ALICE);
    expect(auth.signedIn()).toBe(true);
  });

  it('is signed out when the API answers 401', async () => {
    const loading = auth.load();
    http.expectOne('/api/auth/me').flush(null, { status: 401, statusText: 'Unauthorized' });
    await loading;

    expect(auth.signedIn()).toBe(false);
  });

  it('builds the sign-in URL with the page to return to', () => {
    expect(auth.loginUrl('/diary?page=2')).toBe('/api/auth/login?returnUrl=%2Fdiary%3Fpage%3D2');
  });

  it('signs out and goes to the sign-in page', async () => {
    const loading = auth.load();
    http.expectOne('/api/auth/me').flush(ALICE);
    await loading;
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);

    const logout = auth.logout();
    const request = http.expectOne('/api/auth/logout');
    expect(request.request.method).toBe('POST');
    request.flush(null, { status: 204, statusText: 'No Content' });
    await logout;

    expect(auth.signedIn()).toBe(false);
    expect(navigate).toHaveBeenCalledWith(['/'], { queryParams: {} });
  });
});
