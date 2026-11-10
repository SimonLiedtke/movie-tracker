import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { ActivityLogEntry, PagedResponse } from '../../core/api/models';
import { ActivityLog } from './activity-log';

const entry = (overrides: Partial<ActivityLogEntry>): ActivityLogEntry => ({
  id: 'a1',
  type: 'movieAdded',
  occurredAt: '2026-11-05T21:30:00Z',
  movieId: null,
  movieTitle: null,
  detail: null,
  ...overrides,
});

const VIEWING = entry({
  id: 'a3',
  type: 'diaryEntryAdded',
  occurredAt: '2026-11-07T20:15:00Z',
  movieId: 'm1',
  movieTitle: 'Alien',
  detail: 'Watched on 2026-11-07 · 4.5 stars',
});
const NOTE = entry({
  id: 'a2',
  type: 'watchlistNoteChanged',
  movieId: 'm2',
  movieTitle: 'Heat',
  detail: 'Note: with Sam',
});
const DELETED = entry({ id: 'a1', type: 'movieDeleted', movieTitle: 'Home Video' });
const IMPORT = entry({
  id: 'a0',
  type: 'letterboxdImported',
  detail: 'Diary: 12 imported, 3 skipped · Watchlist: 5 imported, 0 skipped',
});

const paged = (
  items: ActivityLogEntry[],
  { page = 1, totalCount = items.length }: { page?: number; totalCount?: number } = {},
): PagedResponse<ActivityLogEntry> => ({ items, page, pageSize: 50, totalCount });

describe('ActivityLog', () => {
  let http: HttpTestingController;
  let harness: RouterTestingHarness;

  beforeEach(async () => {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([{ path: 'activity', component: ActivityLog }]),
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
  const rows = () => Array.from(page().querySelectorAll('tbody tr'));

  async function open(
    target = '/activity',
    request = '/api/activity-log?page=1&pageSize=50',
    response = paged([VIEWING, NOTE, DELETED, IMPORT]),
  ) {
    await harness.navigateByUrl(target, ActivityLog);
    http.expectOne(request).flush(response);
    await harness.fixture.whenStable();
    harness.detectChanges();
  }

  it('lists the actions with their time, what was done, the movie and the details', async () => {
    await open();

    expect(rows().length).toBe(4);
    const first = rows()[0];
    expect(first.querySelector('time')!.getAttribute('datetime')).toBe('2026-11-07T20:15:00Z');
    expect(first.querySelector('time')!.textContent).toContain('2026');
    expect(first.textContent).toContain('Logged a viewing');
    expect(first.textContent).toContain('Alien');
    expect(first.textContent).toContain('Watched on 2026-11-07 · 4.5 stars');
    expect(rows()[1].textContent).toContain('Changed a watchlist note');
    expect(rows()[1].textContent).toContain('Note: with Sam');
    expect(text()).toContain('4 actions');
  });

  it('links to a movie that still exists, and only names one that was deleted', async () => {
    await open();

    expect(rows()[0].querySelector('a')!.getAttribute('href')).toBe('/movies/m1');
    expect(rows()[2].querySelector('a')).toBeNull();
    expect(rows()[2].textContent).toContain('Deleted a movie');
    expect(rows()[2].textContent).toContain('Home Video');
  });

  it('shows an action that is not about a movie without one', async () => {
    await open();

    expect(rows()[3].textContent).toContain('Imported from Letterboxd');
    expect(rows()[3].querySelector('a')).toBeNull();
  });

  it('says so when nothing was done yet', async () => {
    await open('/activity', '/api/activity-log?page=1&pageSize=50', paged([]));

    expect(text()).toContain('Nothing yet');
    expect(rows().length).toBe(0);
  });

  it('shows the requested page and goes to another one with the pagination', async () => {
    await open(
      '/activity?page=2',
      '/api/activity-log?page=2&pageSize=50',
      paged([VIEWING], { page: 2, totalCount: 120 }),
    );
    expect(text()).toContain('120 actions');

    const next = page().querySelector<HTMLButtonElement>('.pagination-next')!;
    next.click();
    await harness.fixture.whenStable();
    http
      .expectOne('/api/activity-log?page=3&pageSize=50')
      .flush(paged([NOTE], { page: 3, totalCount: 120 }));
    await harness.fixture.whenStable();
    harness.detectChanges();

    expect(url()).toBe('/activity?page=3');
    expect(rows()[0].textContent).toContain('Changed a watchlist note');
  });

  it('leaves the page out of the URL on the first page', async () => {
    await open(
      '/activity?page=2',
      '/api/activity-log?page=2&pageSize=50',
      paged([VIEWING], { page: 2, totalCount: 120 }),
    );

    page().querySelector<HTMLButtonElement>('.pagination-previous')!.click();
    await harness.fixture.whenStable();
    http
      .expectOne('/api/activity-log?page=1&pageSize=50')
      .flush(paged([VIEWING], { totalCount: 120 }));
    await harness.fixture.whenStable();

    expect(url()).toBe('/activity');
  });

  it('goes to the last page if the requested one is past the end', async () => {
    await harness.navigateByUrl('/activity?page=9', ActivityLog);
    http
      .expectOne('/api/activity-log?page=9&pageSize=50')
      .flush(paged([], { page: 9, totalCount: 120 }));
    await harness.fixture.whenStable();

    http
      .expectOne('/api/activity-log?page=3&pageSize=50')
      .flush(paged([NOTE], { page: 3, totalCount: 120 }));
    await harness.fixture.whenStable();
    harness.detectChanges();

    expect(url()).toBe('/activity?page=3');
    expect(rows().length).toBe(1);
  });

  it('shows the error if the log cannot be loaded', async () => {
    await harness.navigateByUrl('/activity', ActivityLog);
    http
      .expectOne('/api/activity-log?page=1&pageSize=50')
      .flush({ title: 'Boom' }, { status: 500, statusText: 'Server Error' });
    await harness.fixture.whenStable();
    harness.detectChanges();

    expect(page().querySelector('[role=alert]')).not.toBeNull();
  });
});
