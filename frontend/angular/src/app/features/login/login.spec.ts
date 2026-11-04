import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { Login } from './login';

async function render(inputs: { returnUrl?: string; loginError?: string } = {}) {
  TestBed.configureTestingModule({ providers: [provideRouter([]), provideHttpClient()] });
  const fixture = TestBed.createComponent(Login);
  for (const [name, value] of Object.entries(inputs)) {
    fixture.componentRef.setInput(name, value);
  }
  await fixture.whenStable();
  return fixture.nativeElement as HTMLElement;
}

describe('Login', () => {
  it('links to the Google sign-in and returns to the movies by default', async () => {
    const page = await render();
    const link = page.querySelector('a.button') as HTMLAnchorElement;

    expect(link.textContent).toContain('Sign in with Google');
    expect(link.getAttribute('href')).toBe('/api/auth/login?returnUrl=%2Fmovies');
    expect(page.querySelector('.notification')).toBeNull();
  });

  it('returns to the page the user wanted to see', async () => {
    const page = await render({ returnUrl: '/watchlist' });

    expect(page.querySelector('a.button')?.getAttribute('href')).toBe(
      '/api/auth/login?returnUrl=%2Fwatchlist',
    );
  });

  it('explains a failed sign-in', async () => {
    const page = await render({ loginError: '1' });

    expect(page.querySelector('.notification')?.textContent).toContain('did not work');
  });
});
