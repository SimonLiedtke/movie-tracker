import { mount } from '@vue/test-utils';
import { describe, expect, it } from 'vitest';
import { createHttp } from '../../core/api/http';
import { createAuth } from '../../core/auth/auth';
import { createTestRouter, provideServices } from '../../test/services';
import Login from './Login.vue';

function render(props: { returnUrl?: string; loginError?: string } = {}) {
  const auth = createAuth({ http: createHttp(), navigate: () => undefined });
  return mount(Login, {
    props,
    global: { plugins: [createTestRouter(), provideServices({ auth })] },
  });
}

describe('Login', () => {
  it('links to the Google sign-in and returns to the movies by default', () => {
    const page = render();
    const link = page.get('a.button');

    expect(link.text()).toContain('Sign in with Google');
    expect(link.attributes('href')).toBe('/api/auth/login?returnUrl=%2Fmovies');
    expect(page.find('.notification').exists()).toBe(false);
  });

  it('returns to the page the user wanted to see', () => {
    const page = render({ returnUrl: '/watchlist' });

    expect(page.get('a.button').attributes('href')).toBe('/api/auth/login?returnUrl=%2Fwatchlist');
  });

  it('only returns to pages of the app', () => {
    const page = render({ returnUrl: 'https://evil.example.com' });

    expect(page.get('a.button').attributes('href')).toBe('/api/auth/login?returnUrl=%2Fmovies');
  });

  it('explains a failed sign-in', () => {
    const page = render({ loginError: '1' });

    expect(page.get('.notification').text()).toContain('did not work');
  });
});
