import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { WatchlistPage, WatchlistItem } from '../../core/api/models';
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
  tags: [],
  genres: [],
  runtimeMinutes: null,
});
const ALIEN: WatchlistItem = {
  ...item('m1', 'Alien'),
  genres: [
    { id: 27, name: 'Horror' },
    { id: 878, name: 'Science Fiction' },
  ],
  runtimeMinutes: 117,
};
const HEAT: WatchlistItem = {
  ...item('m2', 'Heat'),
  genres: [{ id: 18, name: 'Drama' }],
  runtimeMinutes: 170,
};

const paged = (
  items: WatchlistItem[],
  {
    page = 1,
    totalCount = items.length,
    unfilteredCount,
  }: { page?: number; totalCount?: number; unfilteredCount?: number } = {},
): WatchlistPage => ({
  items,
  page,
  pageSize: 20,
  totalCount,
  unfilteredCount: unfilteredCount ?? totalCount,
});

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
    Array.from(page().querySelectorAll('article .media-content p:first-child a')).map(
      (a) => a.textContent,
    );

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
    response: WatchlistPage,
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

  it('counts the movies of the filter and all movies on the watchlist, so that its effect is clear', async () => {
    await open(
      '/watchlist?genreId=27&genreId=878&genreMatch=all',
      '&genreId=27&genreId=878&genreMatch=all',
      paged([ALIEN], { totalCount: 14, unfilteredCount: 52 }),
    );

    expect(text()).toContain('14 of 52 movies to watch with all of the selected genres');
  });

  it('says "of 1 movie" if the watchlist has one movie only, and "0 of" if none matches', async () => {
    await open(
      '/watchlist?genreId=18',
      '&genreId=18&genreMatch=any',
      paged([], { unfilteredCount: 1 }),
    );

    expect(text()).toContain('0 of 1 movie to watch with any of the selected genres');
  });

  it('shows no "of" without a filter', async () => {
    await open();

    expect(text()).toContain('2 movies to watch');
    expect(text()).not.toMatch(/\d of \d/);
  });

  it('selects several genres, and deselects them again', async () => {
    await open();

    await click(
      buttonLabelled('genre-label', 'Horror'),
      '/watchlist?genreId=27',
      '&genreId=27&genreMatch=any',
      paged([ALIEN], { unfilteredCount: 52 }),
    );
    expect(titles()).toEqual(['Alien']);
    expect(text()).toContain('1 of 52 movies to watch with any of the selected genres');

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

  it('does not load the list again when the match mode changes while no genre is selected', async () => {
    await open();

    buttonLabelled('genre-match-label', 'All of them').click();
    await harness.fixture.whenStable();

    // The URL and the buttons follow, but the list is the same: no request, no "loading".
    expect(url()).toBe('/watchlist?genreMatch=all');
    expect(buttonLabelled('genre-match-label', 'All of them').getAttribute('aria-pressed')).toBe(
      'true',
    );
    expect(text()).not.toContain('loading');
    expect(titles()).toEqual(['Alien', 'Heat']);

    buttonLabelled('genre-match-label', 'Any of them').click();
    await harness.fixture.whenStable();

    expect(url()).toBe('/watchlist');
    expect(text()).not.toContain('loading');
  });

  it('loads the list again when the match mode changes while genres are selected', async () => {
    await open('/watchlist?genreId=27', '&genreId=27&genreMatch=any', paged([ALIEN]));

    await click(
      buttonLabelled('genre-match-label', 'All of them'),
      '/watchlist?genreId=27&genreMatch=all',
      '&genreId=27&genreMatch=all',
      paged([ALIEN]),
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

    expect(text()).toContain('No movie on your watchlist matches the filter.');
    expect(text()).not.toContain('Your watchlist is empty');

    await click(
      page().querySelector<HTMLElement>('.notification button')!,
      '/watchlist',
      '',
      paged([HEAT]),
    );
  });

  describe('runtime and date filters', () => {
    const input = (
      id:
        'released-from' | 'released-to' | 'added-from' | 'added-to' | 'runtime-from' | 'runtime-to',
    ) => page().querySelector<HTMLInputElement>(`#${id}`)!;
    const clearButton = () =>
      Array.from(page().querySelectorAll<HTMLButtonElement>('button')).find(
        (b) => b.textContent!.trim() === 'Clear filter',
      );

    /** Enters the date, and answers the request that follows. */
    async function enter(
      id:
        'released-from' | 'released-to' | 'added-from' | 'added-to' | 'runtime-from' | 'runtime-to',
      value: string,
      expectedUrl: string,
      filter: string,
      response: WatchlistPage,
    ) {
      input(id).value = value;
      input(id).dispatchEvent(new Event('change'));
      await harness.fixture.whenStable();
      expect(url()).toBe(expectedUrl);
      listRequest(filter).flush(response);
      await harness.fixture.whenStable();
    }

    it('has no dates, and no filter, to begin with', async () => {
      await open();

      expect(input('released-from').value).toBe('');
      expect(input('released-to').value).toBe('');
      expect(clearButton()).toBeUndefined();
    });

    it('lists the movies released from a day on, and says so', async () => {
      await open();

      await enter(
        'released-from',
        '1979-05-25',
        '/watchlist?releasedFrom=1979-05-25',
        '&releasedFrom=1979-05-25',
        paged([ALIEN], { unfilteredCount: 52 }),
      );

      expect(text()).toContain('1 of 52 movies to watch released from May 25, 1979');
      expect(input('released-to').min).toBe('1979-05-25');
      expect(clearButton()).toBeDefined();
    });

    it('lists the movies released until a day, and in a period', async () => {
      await open();

      await enter(
        'released-to',
        '1989-12-31',
        '/watchlist?releasedTo=1989-12-31',
        '&releasedTo=1989-12-31',
        paged([ALIEN], { unfilteredCount: 52 }),
      );
      expect(text()).toContain('1 of 52 movies to watch released until December 31, 1989');
      expect(input('released-from').max).toBe('1989-12-31');

      await enter(
        'released-from',
        '1979-01-01',
        '/watchlist?releasedTo=1989-12-31&releasedFrom=1979-01-01',
        '&releasedFrom=1979-01-01&releasedTo=1989-12-31',
        paged([ALIEN], { unfilteredCount: 52 }),
      );
      expect(text()).toContain(
        '1 of 52 movies to watch released from January 1, 1979 to December 31, 1989',
      );
    });

    it('removes a date that is emptied', async () => {
      await open('/watchlist?releasedFrom=1979-01-01', '&releasedFrom=1979-01-01', paged([ALIEN]));

      await enter('released-from', '', '/watchlist', '', paged([ALIEN, HEAT]));

      expect(clearButton()).toBeUndefined();
    });

    it('shows the dates of the URL together with the genres', async () => {
      await open(
        '/watchlist?genreId=27&genreMatch=all&releasedFrom=1979-01-01&releasedTo=1989-12-31',
        '&genreId=27&genreMatch=all&releasedFrom=1979-01-01&releasedTo=1989-12-31',
        paged([ALIEN], { unfilteredCount: 9 }),
      );

      expect(input('released-from').value).toBe('1979-01-01');
      expect(input('released-to').value).toBe('1989-12-31');
      expect(text()).toContain(
        '1 of 9 movies to watch with all of the selected genres, released from January 1, 1979 to December 31, 1989',
      );
    });

    it('keeps the dates when the page or the genres change', async () => {
      await open(
        '/watchlist?releasedFrom=1979-01-01',
        '&releasedFrom=1979-01-01',
        paged([HEAT], { totalCount: 45 }),
      );

      await click(
        page().querySelectorAll<HTMLElement>('.pagination-link')[2],
        '/watchlist?releasedFrom=1979-01-01&page=3',
        '&releasedFrom=1979-01-01',
        paged([ALIEN], { page: 3, totalCount: 45 }),
        3,
      );
      await click(
        buttonLabelled('genre-label', 'Horror'),
        '/watchlist?releasedFrom=1979-01-01&genreId=27',
        '&genreId=27&genreMatch=any&releasedFrom=1979-01-01',
        paged([ALIEN]),
      );
    });

    it('clears the dates together with the genres', async () => {
      await open(
        '/watchlist?genreId=27&releasedFrom=1979-01-01&releasedTo=1989-12-31',
        '&genreId=27&genreMatch=any&releasedFrom=1979-01-01&releasedTo=1989-12-31',
        paged([ALIEN]),
      );

      await click(clearButton()!, '/watchlist', '', paged([ALIEN, HEAT]));

      expect(input('released-from').value).toBe('');
      expect(input('released-to').value).toBe('');
    });

    it('lists the movies added from a day on, until a day, and in a period, and says so', async () => {
      await open();

      await enter(
        'added-from',
        '2026-11-01',
        '/watchlist?addedFrom=2026-11-01',
        '&addedFrom=2026-11-01',
        paged([ALIEN], { unfilteredCount: 52 }),
      );
      expect(text()).toContain('1 of 52 movies to watch added from November 1, 2026');
      expect(input('added-to').min).toBe('2026-11-01');

      await enter(
        'added-to',
        '2026-11-05',
        '/watchlist?addedFrom=2026-11-01&addedTo=2026-11-05',
        '&addedFrom=2026-11-01&addedTo=2026-11-05',
        paged([ALIEN], { unfilteredCount: 52 }),
      );
      expect(text()).toContain('added from November 1, 2026 to November 5, 2026');
      expect(input('added-from').max).toBe('2026-11-05');
    });

    it('lists the movies added until a day', async () => {
      await open();

      await enter(
        'added-to',
        '2026-11-05',
        '/watchlist?addedTo=2026-11-05',
        '&addedTo=2026-11-05',
        paged([ALIEN], { unfilteredCount: 52 }),
      );

      expect(text()).toContain('1 of 52 movies to watch added until November 5, 2026');
    });

    it('combines both periods with the genres, and shows them from the URL', async () => {
      await open(
        '/watchlist?genreId=27&releasedFrom=1979-01-01&addedTo=2026-11-05',
        '&genreId=27&genreMatch=any&releasedFrom=1979-01-01&addedTo=2026-11-05',
        paged([ALIEN], { unfilteredCount: 9 }),
      );

      expect(input('released-from').value).toBe('1979-01-01');
      expect(input('added-from').value).toBe('');
      expect(input('added-to').value).toBe('2026-11-05');
      expect(text()).toContain(
        '1 of 9 movies to watch with any of the selected genres, released from January 1, 1979, added until November 5, 2026',
      );
    });

    it('clears the added dates, too', async () => {
      await open(
        '/watchlist?addedFrom=2026-11-01&addedTo=2026-11-05',
        '&addedFrom=2026-11-01&addedTo=2026-11-05',
        paged([ALIEN]),
      );

      await click(clearButton()!, '/watchlist', '', paged([ALIEN, HEAT]));

      expect(input('added-from').value).toBe('');
      expect(input('added-to').value).toBe('');
    });

    describe('runtime', () => {
      const presetButtons = () =>
        Array.from(
          page().querySelectorAll<HTMLButtonElement>('[aria-labelledby=runtime-label] button'),
        );
      const preset = (label: string) =>
        presetButtons().find((b) => b.textContent!.trim() === label)!;
      const pressed = () =>
        presetButtons()
          .filter((b) => b.getAttribute('aria-pressed') === 'true')
          .map((b) => b.textContent!.trim());

      it('offers quick picks for the usual lengths, and fields for any other, none set to begin with', async () => {
        await open();

        expect(presetButtons().map((b) => b.textContent!.trim())).toEqual([
          'Under 90 min',
          '90 to 120 min',
          'Over 120 min',
        ]);
        expect(pressed()).toEqual([]);
        expect(input('runtime-from').value).toBe('');
        expect(input('runtime-to').value).toBe('');
        expect(input('runtime-from').min).toBe('1');
      });

      it('lists the movies of exactly the entered length range, and says so', async () => {
        await open();

        await enter(
          'runtime-from',
          '23',
          '/watchlist?runtimeFrom=23',
          '&runtimeFrom=23',
          paged([ALIEN], { unfilteredCount: 52 }),
        );
        expect(text()).toContain('1 of 52 movies to watch runtime of at least 23 minutes');

        await enter(
          'runtime-to',
          '148',
          '/watchlist?runtimeFrom=23&runtimeTo=148',
          '&runtimeFrom=23&runtimeTo=148',
          paged([ALIEN], { unfilteredCount: 52 }),
        );
        expect(text()).toContain('1 of 52 movies to watch runtime of 23 to 148 minutes');
        expect(pressed()).toEqual([]);
      });

      it('lists the movies that are at most as long as entered', async () => {
        await open();

        await enter(
          'runtime-to',
          '148',
          '/watchlist?runtimeTo=148',
          '&runtimeTo=148',
          paged([ALIEN], { unfilteredCount: 52 }),
        );

        expect(text()).toContain('1 of 52 movies to watch runtime of at most 148 minutes');
      });

      it('selects a quick pick, and deselects it with a second click', async () => {
        await open();

        await click(
          preset('Under 90 min'),
          '/watchlist?runtimeTo=89',
          '&runtimeTo=89',
          paged([ALIEN]),
        );
        expect(pressed()).toEqual(['Under 90 min']);
        expect(input('runtime-to').value).toBe('89');
        expect(input('runtime-from').value).toBe('');

        await click(preset('Under 90 min'), '/watchlist', '', paged([HEAT]));
        expect(pressed()).toEqual([]);
        expect(input('runtime-to').value).toBe('');
      });

      it('replaces the runtime of another quick pick or of the fields', async () => {
        await open(
          '/watchlist?runtimeFrom=23&runtimeTo=148',
          '&runtimeFrom=23&runtimeTo=148',
          paged([ALIEN]),
        );

        await click(
          preset('90 to 120 min'),
          '/watchlist?runtimeFrom=90&runtimeTo=120',
          '&runtimeFrom=90&runtimeTo=120',
          paged([ALIEN]),
        );
        expect(pressed()).toEqual(['90 to 120 min']);

        // "Over 120 min" has no upper end: the old one is removed.
        await click(
          preset('Over 120 min'),
          '/watchlist?runtimeFrom=121',
          '&runtimeFrom=121',
          paged([HEAT]),
        );
        expect(pressed()).toEqual(['Over 120 min']);
        expect(input('runtime-to').value).toBe('');
      });

      it('highlights the quick pick if the fields have the same values', async () => {
        await open(
          '/watchlist?runtimeFrom=90&runtimeTo=120',
          '&runtimeFrom=90&runtimeTo=120',
          paged([ALIEN]),
        );

        expect(pressed()).toEqual(['90 to 120 min']);
      });

      it('ignores what is not a number of minutes in the URL', async () => {
        await open('/watchlist?runtimeFrom=abc&runtimeTo=-5', '', paged([ALIEN]));

        expect(input('runtime-from').value).toBe('');
        expect(clearButton()).toBeUndefined();
      });

      it('keeps it with the other filters, and clears it with them', async () => {
        await open(
          '/watchlist?genreId=27&runtimeFrom=90&releasedTo=1989-12-31',
          '&genreId=27&genreMatch=any&releasedTo=1989-12-31&runtimeFrom=90',
          paged([ALIEN], { unfilteredCount: 9 }),
        );
        expect(text()).toContain(
          '1 of 9 movies to watch with any of the selected genres, runtime of at least 90 minutes, released until December 31, 1989',
        );

        await click(clearButton()!, '/watchlist', '', paged([ALIEN, HEAT]));

        expect(input('runtime-from').value).toBe('');
      });
    });

    it('says when no movie was released in the period', async () => {
      await open(
        '/watchlist?releasedTo=1900-01-01',
        '&releasedTo=1900-01-01',
        paged([], { unfilteredCount: 3 }),
      );

      expect(text()).toContain('0 of 3 movies to watch released until January 1, 1900');
      expect(text()).toContain('No movie on your watchlist matches the filter.');
    });
  });

  describe('title search', () => {
    const field = () => page().querySelector<HTMLInputElement>('#search')!;
    const clearButton = () =>
      Array.from(page().querySelectorAll<HTMLButtonElement>('button')).find(
        (b) => b.textContent!.trim() === 'Clear filter',
      );

    /** Types the text, and waits until the debounce time has passed. */
    async function type(value: string) {
      await typeWithoutWaiting(value);
      await new Promise((resolve) => setTimeout(resolve, 400));
      await harness.fixture.whenStable();
    }

    async function typeWithoutWaiting(value: string) {
      field().value = value;
      field().dispatchEvent(new Event('input'));
      await harness.fixture.whenStable();
    }

    it('lists the movies with the text in their title, once the user has stopped typing', async () => {
      await open();

      await typeWithoutWaiting('al');
      await typeWithoutWaiting('ali');
      await typeWithoutWaiting('alien');
      // Nothing yet: the user may still be typing.
      expect(url()).toBe('/watchlist');

      await vi.waitFor(() => expect(url()).toBe('/watchlist?search=alien'));
      listRequest('&search=alien').flush(paged([ALIEN], { unfilteredCount: 52 }));
      await harness.fixture.whenStable();
      expect(text()).toContain('1 of 52 movies to watch matching "alien"');
      expect(field().value).toBe('alien');
      expect(clearButton()).toBeDefined();
    });

    it('starts again at page 1, and keeps the other filters', async () => {
      await open(
        '/watchlist?genreId=27&page=2',
        '&genreId=27&genreMatch=any',
        paged([HEAT], { page: 2, totalCount: 45 }),
      );

      await type('heat');

      expect(url()).toBe('/watchlist?genreId=27&search=heat');
      listRequest('&genreId=27&genreMatch=any&search=heat').flush(paged([HEAT]));
    });

    it('shows the search of the URL, and sends it without the spaces around it', async () => {
      await open('/watchlist?search=alien%20', '&search=alien', paged([ALIEN]));

      expect(field().value).toBe('alien ');
      expect(text()).toContain('1 of 1 movie to watch matching "alien"');
    });

    it('does not filter by spaces only, and removes the search from the URL when emptied', async () => {
      await open();

      await type('   ');
      expect(url()).toBe('/watchlist');
      expect(clearButton()).toBeUndefined();

      await type('alien');
      listRequest('&search=alien').flush(paged([ALIEN]));
      await harness.fixture.whenStable();
      await type('');

      expect(url()).toBe('/watchlist');
      listRequest().flush(paged([ALIEN, HEAT]));
    });

    it('is cleared with the other filters, and says when no title matches', async () => {
      await open('/watchlist?search=zzz', '&search=zzz', paged([], { unfilteredCount: 3 }));
      expect(text()).toContain('0 of 3 movies to watch matching "zzz"');
      expect(text()).toContain('No movie on your watchlist matches the filter.');

      await click(clearButton()!, '/watchlist', '', paged([ALIEN, HEAT]));

      expect(field().value).toBe('');
    });

    it('is combined with the genres in the description', async () => {
      await open(
        '/watchlist?search=alien&genreId=27',
        '&genreId=27&genreMatch=any&search=alien',
        paged([ALIEN], { unfilteredCount: 9 }),
      );

      expect(text()).toContain(
        '1 of 9 movies to watch matching "alien", with any of the selected genres',
      );
    });
  });

  describe('runtime of a movie', () => {
    const line = (article: Element) =>
      article.querySelector('.is-size-7.has-text-grey')!.textContent!.replace(/\s+/g, ' ').trim();

    it('is shown after the date added, and left out if unknown', async () => {
      await open('/watchlist', '', paged([HEAT, ALIEN, item('m3', 'Unknown')]));

      const articles = Array.from(page().querySelectorAll('article'));
      expect(line(articles[0])).toBe('Added 2026-11-05 · 170 min');
      expect(line(articles[1])).toBe('Added 2026-11-05 · 117 min');
      expect(line(articles[2])).toBe('Added 2026-11-05');
    });
  });

  describe('genres of a movie', () => {
    const tags = (article: Element) =>
      Array.from(article.querySelectorAll<HTMLAnchorElement>('.tags a.tag'));

    it('shows them as tags like the movie page, without tags for a movie that has no genres', async () => {
      await open('/watchlist', '', paged([ALIEN, HEAT, item('m3', 'Unknown')]));

      const articles = Array.from(page().querySelectorAll('article'));
      expect(tags(articles[0]).map((t) => t.textContent!.trim())).toEqual([
        'Horror',
        'Science Fiction',
      ]);
      expect(tags(articles[1]).map((t) => t.textContent!.trim())).toEqual(['Drama']);
      expect(tags(articles[0])[0].classList).toContain('is-light');
      expect(articles[2].querySelector('.tags')).toBeNull();
    });

    it('filters by the genre when clicked, leaving the match mode alone, from page 1', async () => {
      await open(
        '/watchlist?genreId=18&genreMatch=all&page=2',
        '&genreId=18&genreMatch=all',
        paged([ALIEN], { page: 2, totalCount: 21 }),
      );
      const horror = tags(page().querySelector('article')!).find(
        (t) => t.textContent!.trim() === 'Horror',
      )!;
      expect(horror.getAttribute('href')).toBe('/watchlist?genreId=27&genreMatch=all');

      await click(
        horror,
        '/watchlist?genreId=27&genreMatch=all',
        '&genreId=27&genreMatch=all',
        paged([ALIEN]),
      );

      expect(selectedGenres()).toEqual(['Horror']);
    });

    it('shows the genres of the filter highlighted', async () => {
      await open('/watchlist?genreId=27', '&genreId=27&genreMatch=any', paged([ALIEN]));

      const [horror, scienceFiction] = tags(page().querySelector('article')!);
      expect(horror.classList).not.toContain('is-light');
      expect(scienceFiction.classList).toContain('is-light');
    });
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
