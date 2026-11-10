import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { PagedResponse, WatchlistItem } from '../../core/api/models';
import { Watchlist } from './watchlist';

const GENRES = [
  { id: 27, name: 'Horror' },
  { id: 878, name: 'Science Fiction' },
  { id: 18, name: 'Drama' },
];

const item = (id: string, title: string): WatchlistItem => ({
  movie: { id, title, releaseDate: '1979-05-25', posterUrl: null },
  addedAt: '2026-11-05T21:30:00Z',
  note: null,
});
const ALIEN = item('m1', 'Alien');
const HEAT = item('m2', 'Heat');

const paged = (
  items: WatchlistItem[],
  { page = 1, totalCount = items.length } = {},
): PagedResponse<WatchlistItem> => ({ items, page, pageSize: 20, totalCount });

describe('Watchlist genre filter', () => {
  let http: HttpTestingController;
  let harness: RouterTestingHarness;

  beforeEach(async () => {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([{ path: 'watchlist', component: Watchlist }]),
        provideHttpClient(),
        provideHttpClientTesting(),
      ],
    });
    http = TestBed.inject(HttpTestingController);
    harness = await RouterTestingHarness.create();
  });

  afterEach(() => http.verify());

  const page = () => harness.routeNativeElement as HTMLElement;
  const text = () => page().textContent!.replace(/\s+/g, ' ');
  const url = () => TestBed.inject(Router).url;
  const buttons = (group: string) =>
    Array.from(page().querySelectorAll<HTMLButtonElement>(`[aria-labelledby=${group}] button`));
  const buttonLabelled = (group: string, label: string) =>
    buttons(group).find((b) => b.textContent!.trim() === label)!;
  const selectedGenres = () =>
    buttons('genre-label')
      .filter((b) => b.getAttribute('aria-pressed') === 'true')
      .map((b) => b.textContent!.trim());
  const titles = () =>
    Array.from(page().querySelectorAll('article .media-content a')).map((a) => a.textContent);

  /** `filter` is the rest of the query string, e.g. `&genreId=27&genreMatch=any`. */
  const listRequest = (filter = '', pageNumber = 1) =>
    http.expectOne(`/api/watchlist?page=${pageNumber}&pageSize=20${filter}`);

  async function open(target = '/watchlist', filter = '', response = paged([ALIEN, HEAT])) {
    const navigated = harness.navigateByUrl(target, Watchlist);
    await navigated;
    http.expectOne('/api/genres').flush(GENRES);
    listRequest(filter, response.page).flush(response);
    await harness.fixture.whenStable();
  }

  /** Clicks the button and answers the request that follows. */
  async function click(
    button: HTMLElement,
    expectedUrl: string,
    filter: string,
    response: PagedResponse<WatchlistItem>,
    pageNumber = 1,
  ) {
    button.click();
    await harness.fixture.whenStable();
    expect(url()).toBe(expectedUrl);
    listRequest(filter, pageNumber).flush(response);
    await harness.fixture.whenStable();
  }

  it('offers every genre, none selected, and "any of them" to combine them', async () => {
    await open();

    expect(buttons('genre-label').map((b) => b.textContent!.trim())).toEqual([
      'Horror',
      'Science Fiction',
      'Drama',
    ]);
    expect(selectedGenres()).toEqual([]);
    expect(buttonLabelled('genre-match-label', 'Any of them').getAttribute('aria-pressed')).toBe(
      'true',
    );
    expect(buttonLabelled('genre-match-label', 'All of them').getAttribute('aria-pressed')).toBe(
      'false',
    );
    expect(text()).not.toContain('Clear filter');
  });

  it('selects several genres, and deselects them again', async () => {
    await open();

    await click(
      buttonLabelled('genre-label', 'Horror'),
      '/watchlist?genreId=27',
      '&genreId=27&genreMatch=any',
      paged([ALIEN]),
    );
    expect(titles()).toEqual(['Alien']);
    expect(text()).toContain('1 movie to watch with any of the selected genres');

    await click(
      buttonLabelled('genre-label', 'Drama'),
      '/watchlist?genreId=27&genreId=18',
      '&genreId=27&genreId=18&genreMatch=any',
      paged([ALIEN, HEAT]),
    );
    expect(selectedGenres()).toEqual(['Horror', 'Drama']);

    await click(
      buttonLabelled('genre-label', 'Horror'),
      '/watchlist?genreId=18',
      '&genreId=18&genreMatch=any',
      paged([HEAT]),
    );
    await click(buttonLabelled('genre-label', 'Drama'), '/watchlist', '', paged([ALIEN, HEAT]));
    expect(selectedGenres()).toEqual([]);
  });

  it('matches all genres instead of any, and back', async () => {
    await open(
      '/watchlist?genreId=27&genreId=878',
      '&genreId=27&genreId=878&genreMatch=any',
      paged([ALIEN]),
    );

    await click(
      buttonLabelled('genre-match-label', 'All of them'),
      '/watchlist?genreId=27&genreId=878&genreMatch=all',
      '&genreId=27&genreId=878&genreMatch=all',
      paged([ALIEN]),
    );
    expect(buttonLabelled('genre-match-label', 'All of them').getAttribute('aria-pressed')).toBe(
      'true',
    );
    expect(text()).toContain('with all of the selected genres');

    // "any" is the default, so it is not in the URL.
    await click(
      buttonLabelled('genre-match-label', 'Any of them'),
      '/watchlist?genreId=27&genreId=878',
      '&genreId=27&genreId=878&genreMatch=any',
      paged([ALIEN, HEAT]),
    );
    expect(buttonLabelled('genre-match-label', 'Any of them').getAttribute('aria-pressed')).toBe(
      'true',
    );
  });

  it('shows the filter of the URL, ignoring what is not a genre ID', async () => {
    await open(
      '/watchlist?genreId=27&genreId=abc&genreId=27&genreId=878&genreMatch=all',
      '&genreId=27&genreId=878&genreMatch=all',
      paged([ALIEN]),
    );

    expect(selectedGenres()).toEqual(['Horror', 'Science Fiction']);
    expect(buttonLabelled('genre-match-label', 'All of them').getAttribute('aria-pressed')).toBe(
      'true',
    );
  });

  it('does not send the match mode without genres', async () => {
    await open('/watchlist?genreMatch=all');
  });

  it('keeps the filter on another page, and starts at page 1 when it changes', async () => {
    await open(
      '/watchlist?genreId=27',
      '&genreId=27&genreMatch=any',
      paged([ALIEN], { totalCount: 45 }),
    );

    await click(
      page().querySelectorAll<HTMLElement>('.pagination-link')[2],
      '/watchlist?genreId=27&page=3',
      '&genreId=27&genreMatch=any',
      paged([HEAT], { page: 3, totalCount: 45 }),
      3,
    );
    await click(
      buttonLabelled('genre-label', 'Drama'),
      '/watchlist?genreId=27&genreId=18',
      '&genreId=27&genreId=18&genreMatch=any',
      paged([HEAT]),
    );
  });

  it('clears the filter, and starts at page 1', async () => {
    await open(
      '/watchlist?genreId=27&genreMatch=all&page=2',
      '&genreId=27&genreMatch=all',
      paged([ALIEN], { page: 2, totalCount: 21 }),
    );

    const clear = Array.from(page().querySelectorAll<HTMLButtonElement>('button')).find(
      (b) => b.textContent!.trim() === 'Clear filter',
    )!;
    await click(clear, '/watchlist', '', paged([ALIEN, HEAT]));

    expect(selectedGenres()).toEqual([]);
    expect(buttonLabelled('genre-match-label', 'Any of them').getAttribute('aria-pressed')).toBe(
      'true',
    );
  });

  it('says when no movie matches, and offers to clear the filter', async () => {
    await open('/watchlist?genreId=18', '&genreId=18&genreMatch=any', paged([]));

    expect(text()).toContain('No movie on your watchlist matches the selected genres.');
    expect(text()).not.toContain('Your watchlist is empty');

    await click(
      page().querySelector<HTMLElement>('.notification button')!,
      '/watchlist',
      '',
      paged([HEAT]),
    );
  });

  it('still shows the watchlist if the genres cannot be loaded', async () => {
    await harness.navigateByUrl('/watchlist', Watchlist);
    http.expectOne('/api/genres').error(new ProgressEvent('error'));
    listRequest().flush(paged([ALIEN]));
    await harness.fixture.whenStable();

    expect(titles()).toEqual(['Alien']);
    expect(buttons('genre-label')).toHaveLength(0);
  });
});
