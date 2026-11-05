import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { MovieRef, MovieSearchResponse } from '../core/api/models';
import { MoviePicker } from './movie-picker';

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
  let fixture: ComponentFixture<MoviePicker>;
  let http: HttpTestingController;
  let picked: MovieRef[];

  beforeEach(async () => {
    TestBed.configureTestingModule({
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()],
    });
    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(MoviePicker);
    fixture.componentRef.setInput('afterManualAdd', 'diary');
    picked = [];
    fixture.componentInstance.picked.subscribe((movie) => picked.push(movie));
    await fixture.whenStable();
  });

  afterEach(() => http.verify());

  const element = () => fixture.nativeElement as HTMLElement;

  async function search(term: string, response = RESPONSE) {
    const input = element().querySelector('input') as HTMLInputElement;
    input.value = term;
    input.dispatchEvent(new Event('input'));
    await new Promise((resolve) => setTimeout(resolve, 300)); // debounce
    http
      .expectOne((r) => r.url === '/api/movies/search' && r.params.get('query') === term)
      .flush(response);
    await fixture.whenStable();
  }

  const items = () =>
    Array.from(element().querySelectorAll('.dropdown-item')).map((item) =>
      item.textContent!.replace(/\s+/g, ' ').trim(),
    );

  it('shows own movies and TMDB results, and offers to add a movie manually', async () => {
    await search('alien');

    expect(items()).toEqual([
      'Alien Home VideoYours',
      'Alien (1979)',
      "Can't find it? Add it manually",
    ]);
    const manualLink = element().querySelector('a[href^="/movies/new"]')!;
    expect(manualLink.getAttribute('href')).toBe('/movies/new?title=alien&next=diary');
  });

  it('says when TMDB is not available', async () => {
    await search('alien', { tmdbAvailable: false, items: [RESPONSE.items[0]] });

    expect(items()[0]).toContain('TMDB search is not available');
  });

  it('picks an own movie without another request', async () => {
    await search('alien');

    (element().querySelectorAll('a.dropdown-item')[0] as HTMLElement).click();
    await fixture.whenStable();

    expect(picked).toEqual([
      { id: 'm1', title: 'Alien Home Video', releaseDate: null, posterUrl: null },
    ]);
    expect((element().querySelector('input') as HTMLInputElement).value).toBe('Alien Home Video');
  });

  it('imports a TMDB movie before picking it', async () => {
    await search('alien');

    (element().querySelectorAll('a.dropdown-item')[1] as HTMLElement).click();
    const request = http.expectOne('/api/movies/tmdb/348');
    expect(request.request.method).toBe('POST');
    request.flush({
      id: 'm2',
      title: 'Alien',
      releaseDate: '1979-05-25',
      posterUrl: 'https://image/alien.jpg',
    });
    await fixture.whenStable();

    expect(picked).toEqual([
      { id: 'm2', title: 'Alien', releaseDate: '1979-05-25', posterUrl: 'https://image/alien.jpg' },
    ]);
    expect(fixture.componentInstance.selected()?.id).toBe('m2');
  });

  it('shows why a TMDB import failed', async () => {
    await search('alien');

    (element().querySelectorAll('a.dropdown-item')[1] as HTMLElement).click();
    http
      .expectOne('/api/movies/tmdb/348')
      .flush({ detail: 'TMDB is down.' }, { status: 502, statusText: 'Bad Gateway' });
    await fixture.whenStable();

    expect(picked).toEqual([]);
    expect(element().querySelector('.help.is-danger')?.textContent).toContain('TMDB is down.');
  });

  it('clears the field after picking when it does not keep the selection', async () => {
    fixture.componentRef.setInput('keepSelection', false);
    await search('alien');

    (element().querySelectorAll('a.dropdown-item')[0] as HTMLElement).click();
    await fixture.whenStable();

    expect(picked.length).toBe(1);
    expect(fixture.componentInstance.selected()).toBeNull();
    expect((element().querySelector('input') as HTMLInputElement).value).toBe('');
  });
});
