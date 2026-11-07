import { flushPromises, mount, type VueWrapper } from '@vue/test-utils';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { createApi } from '../core/api/api';
import { createHttp } from '../core/api/http';
import type { MovieRef, MovieSearchResponse } from '../core/api/models';
import { useFakeBackend } from '../test/fake-backend';
import { createTestRouter, provideServices } from '../test/services';
import MoviePicker from './MoviePicker.vue';

const RESPONSE: MovieSearchResponse = {
  tmdbAvailable: true,
  items: [
    {
      movieId: 'm1',
      tmdbId: null,
      title: 'Alien Home Video',
      originalTitle: null,
      releaseDate: null,
      posterUrl: null,
    },
    {
      movieId: null,
      tmdbId: 348,
      title: 'Alien',
      originalTitle: 'Alien',
      releaseDate: '1979-05-25',
      posterUrl: null,
    },
  ],
};

describe('MoviePicker', () => {
  const backend = useFakeBackend();
  let picker: VueWrapper;
  let picked: MovieRef[];

  function render(props: Record<string, unknown> = {}) {
    picked = [];
    picker = mount(MoviePicker, {
      props: {
        afterManualAdd: 'diary',
        onPicked: (movie: MovieRef) => picked.push(movie),
        ...props,
      },
      global: {
        plugins: [createTestRouter(), provideServices({ api: createApi(createHttp()) })],
      },
    });
  }

  beforeEach(() => {
    // Only the timers: the promises of the fake backend must keep running.
    vi.useFakeTimers({ toFake: ['setTimeout', 'clearTimeout'] });
    render();
  });

  afterEach(() => {
    picker.unmount();
    vi.useRealTimers();
  });

  const input = () => picker.get('input').element;

  async function type(term: string) {
    await picker.get('input').setValue(term);
  }

  const searchRequest = (term: string) =>
    backend.expectOne((r) => r.url === '/api/movies/search' && r.params.get('query') === term);

  async function search(term: string, response = RESPONSE) {
    await type(term);
    await vi.advanceTimersByTimeAsync(300); // debounce
    searchRequest(term).flush(response);
    await flushPromises();
  }

  const items = () =>
    picker.findAll('.dropdown-item').map((item) => item.text().replace(/\s+/g, ' ').trim());

  it('shows own movies and TMDB results, and offers to add a movie manually', async () => {
    await search('alien');

    expect(items()).toEqual([
      'Alien Home VideoYours',
      'Alien (1979)',
      "Can't find it? Add it manually",
    ]);
    expect(picker.get('a[href^="/movies/new"]').attributes('href')).toBe(
      '/movies/new?title=alien&next=diary',
    );
    expect(picker.get('.dropdown').classes()).toContain('is-active');
  });

  it('does not tell the manual form where to continue if it does not know', async () => {
    picker.unmount();
    render({ afterManualAdd: undefined });
    await search('alien');

    expect(picker.get('a[href^="/movies/new"]').attributes('href')).toBe('/movies/new?title=alien');
  });

  it('waits until the user has stopped typing, and does not search for nothing', async () => {
    await type('al');
    await vi.advanceTimersByTimeAsync(200);
    await type('alien ');
    await vi.advanceTimersByTimeAsync(200);
    backend.verify(); // Still waiting.

    await vi.advanceTimersByTimeAsync(100);
    searchRequest('alien').flush(RESPONSE); // Trimmed.
    await flushPromises();
    expect(items()).toHaveLength(3);

    await type('   ');
    await vi.advanceTimersByTimeAsync(300);
    await flushPromises();
    expect(items()).toEqual([]);
    expect(picker.get('.dropdown').classes()).not.toContain('is-active');
  });

  it('says when TMDB is not available', async () => {
    await search('alien', { tmdbAvailable: false, items: [RESPONSE.items[0]] });

    expect(items()[0]).toContain('TMDB search is not available');
  });

  it('says so when the search fails altogether', async () => {
    await type('alien');
    await vi.advanceTimersByTimeAsync(300);
    searchRequest('alien').networkError();
    await flushPromises();

    expect(items()[0]).toContain('TMDB search is not available');
    expect(items()[1]).toBe('No matching movies.');
  });

  it('shows the answer to the latest search only', async () => {
    await type('ali');
    await vi.advanceTimersByTimeAsync(300);
    const outdated = searchRequest('ali');
    await type('alien');
    await vi.advanceTimersByTimeAsync(300);
    const latest = searchRequest('alien');

    latest.flush(RESPONSE);
    await flushPromises();
    outdated.flush({ tmdbAvailable: true, items: [] });
    await flushPromises();

    expect(items()).toHaveLength(3);
  });

  it('closes on Escape and opens again when focused', async () => {
    await search('alien');

    await picker.get('input').trigger('keydown', { key: 'Escape' });
    expect(picker.get('.dropdown').classes()).not.toContain('is-active');

    await picker.get('input').trigger('focus');
    expect(picker.get('.dropdown').classes()).toContain('is-active');
  });

  it('picks an own movie without another request', async () => {
    await search('alien');

    await picker.findAll('a.dropdown-item')[0].trigger('click');

    expect(picked).toEqual([
      { id: 'm1', title: 'Alien Home Video', releaseDate: null, posterUrl: null },
    ]);
    expect((input() as HTMLInputElement).value).toBe('Alien Home Video');
  });

  it('imports a TMDB movie before picking it', async () => {
    await search('alien');

    await picker.findAll('a.dropdown-item')[1].trigger('click');
    const request = backend.expectOne('/api/movies/tmdb/348');
    expect(request.method).toBe('POST');
    request.flush({
      id: 'm2',
      title: 'Alien',
      releaseDate: '1979-05-25',
      posterUrl: 'https://image/alien.jpg',
    });
    await flushPromises();

    const movie = {
      id: 'm2',
      title: 'Alien',
      releaseDate: '1979-05-25',
      posterUrl: 'https://image/alien.jpg',
    };
    expect(picked).toEqual([movie]);
    expect(picker.emitted('update:selected')).toEqual([[movie]]);
  });

  it('locks the field while a TMDB movie is imported', async () => {
    await search('alien');

    await picker.findAll('a.dropdown-item')[1].trigger('click');

    expect(picker.get('input').attributes('disabled')).toBeDefined();
    expect(picker.get('.control').classes()).toContain('is-loading');
    backend.expectOne('/api/movies/tmdb/348').flush({ id: 'm2', title: 'Alien' });
    await flushPromises();
  });

  it('shows why a TMDB import failed', async () => {
    await search('alien');

    await picker.findAll('a.dropdown-item')[1].trigger('click');
    backend
      .expectOne('/api/movies/tmdb/348')
      .flush({ detail: 'TMDB is down.' }, { status: 502, statusText: 'Bad Gateway' });
    await flushPromises();

    expect(picked).toEqual([]);
    expect(picker.get('.help.is-danger').text()).toContain('TMDB is down.');
  });

  it('shows the picked movie and lets the user change it', async () => {
    picker.unmount();
    render({ selected: { id: 'm1', title: 'Alien', releaseDate: '1979-05-25', posterUrl: null } });

    expect((input() as HTMLInputElement).value).toBe('Alien (1979)');
    await picker.get('button').trigger('click');

    expect(picker.emitted('update:selected')).toEqual([[null]]);
  });

  it('clears the field after picking when it does not keep the selection', async () => {
    picker.unmount();
    render({ keepSelection: false });
    await search('alien');

    await picker.findAll('a.dropdown-item')[0].trigger('click');

    expect(picked).toHaveLength(1);
    expect(picker.emitted('update:selected')).toBeUndefined();
    expect((input() as HTMLInputElement).value).toBe('');
    expect(picker.get('.dropdown').classes()).not.toContain('is-active');
  });
});
