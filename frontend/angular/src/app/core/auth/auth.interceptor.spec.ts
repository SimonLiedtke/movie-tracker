import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { Auth } from './auth';
import { authInterceptor } from './auth.interceptor';

describe('authInterceptor', () => {
  let http: HttpClient;
  let backend: HttpTestingController;
  let sessionEnded: ReturnType<typeof vi.fn>;

  beforeEach(() => {
    sessionEnded = vi.fn();
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
        provideRouter([]),
        { provide: Auth, useValue: { sessionEnded } },
      ],
    });
    http = TestBed.inject(HttpClient);
    backend = TestBed.inject(HttpTestingController);
  });

  afterEach(() => backend.verify());

  it('sends the CSRF header to the API only', () => {
    http.post('/api/movies', {}).subscribe();
    http.get('https://example.com/poster.jpg').subscribe();

    expect(backend.expectOne('/api/movies').request.headers.get('X-CSRF')).toBe('1');
    expect(backend.expectOne('https://example.com/poster.jpg').request.headers.has('X-CSRF')).toBe(
      false,
    );
  });

  it('goes to the sign-in page when the session has expired', () => {
    http.get('/api/diary-entries').subscribe({ error: () => undefined });
    backend
      .expectOne('/api/diary-entries')
      .flush(null, { status: 401, statusText: 'Unauthorized' });

    expect(sessionEnded).toHaveBeenCalledOnce();
  });

  it('leaves 401 from the auth endpoints to the Auth service', () => {
    http.get('/api/auth/me').subscribe({ error: () => undefined });
    backend.expectOne('/api/auth/me').flush(null, { status: 401, statusText: 'Unauthorized' });

    expect(sessionEnded).not.toHaveBeenCalled();
  });
});
