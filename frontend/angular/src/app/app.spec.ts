import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { App } from './app';
import { User } from './core/api/models';
import { Auth } from './core/auth/auth';

async function render(user: User | null) {
  await TestBed.configureTestingModule({
    imports: [App],
    providers: [provideRouter([]), { provide: Auth, useValue: { user: signal(user) } }],
  }).compileComponents();
  const fixture = TestBed.createComponent(App);
  await fixture.whenStable();
  return fixture.nativeElement as HTMLElement;
}

describe('App', () => {
  it('renders the navigation and the user for signed-in users', async () => {
    const page = await render({
      id: '1',
      email: 'alice@example.com',
      name: 'Alice',
      pictureUrl: null,
    });

    const links = [...page.querySelectorAll('.navbar-start a')];
    expect(links.map((link) => link.textContent?.trim())).toEqual([
      'My movies',
      'Diary',
      'Watchlist',
      'Activity',
      'Import',
    ]);
    expect(page.querySelector('.navbar-end')?.textContent).toContain('Alice');
    expect(page.querySelector('.navbar-end button')?.textContent).toContain('Sign out');
  });

  it('hides the navigation on the sign-in page', async () => {
    const page = await render(null);

    expect(page.querySelector('nav.navbar')).toBeNull();
  });
});
