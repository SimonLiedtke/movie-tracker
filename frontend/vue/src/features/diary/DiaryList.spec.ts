import { flushPromises, type VueWrapper } from '@vue/test-utils';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { createApi } from '../../core/api/api';
import { createHttp } from '../../core/api/http';
import type { DiaryEntry, PagedResponse } from '../../core/api/models';
import { diaryListProps } from '../../route-props';
import { useFakeBackend } from '../../test/fake-backend';
import { mountRoute } from '../../test/services';
import DiaryList from './DiaryList.vue';

const entry = (id: string, watchedOn: string, changes: Partial<DiaryEntry> = {}): DiaryEntry => ({
  id,
  movie: { id: 'm1', title: 'Alien', releaseDate: '1979-05-25', posterUrl: null },
  watchedOn,
  rating: null,
  review: null,
  isRewatch: false,
  liked: false,
  createdAt: `${watchedOn}T20:00:00Z`,
  ...changes,
});

const ENTRIES = [
  entry('e1', '2026-11-07', { rating: 9, review: 'Still scary.', isRewatch: true, liked: true }),
  entry('e2', '2026-11-01', {
    movie: { id: 'm2', title: 'Heat', releaseDate: null, posterUrl: null },
  }),
  entry('e3', '2026-10-31', { rating: 3 }),
];

const paged = (
  items: DiaryEntry[],
  { page = 1, totalCount = items.length } = {},
): PagedResponse<DiaryEntry> => ({ items, page, pageSize: 25, totalCount });

describe('DiaryList', () => {
  const backend = useFakeBackend();

  afterEach(() => vi.restoreAllMocks());

  async function open(url = '/diary') {
    return mountRoute(
      url,
      { path: '/diary', component: DiaryList, props: diaryListProps },
      { api: createApi(createHttp()) },
    );
  }

  const diaryRequest = (query: string) => backend.expectOne(`/api/diary-entries?${query}`);
  const text = (page: VueWrapper) => page.text().replace(/\s+/g, ' ');

  async function openWith(entries: DiaryEntry[], url = '/diary', query = 'page=1&pageSize=25') {
    const opened = await open(url);
    diaryRequest(query).flush(paged(entries));
    await flushPromises();
    return opened;
  }

  it('groups the viewings by month', async () => {
    const { page } = await open();
    expect(page.find('progress').exists()).toBe(true);
    diaryRequest('page=1&pageSize=25').flush(paged(ENTRIES));
    await flushPromises();

    expect(page.find('progress').exists()).toBe(false);
    expect(text(page)).toContain('3 entries');
    expect(page.findAll('tbody').map((group) => group.get('th').text())).toEqual([
      'November 2026',
      'October 2026',
    ]);
    expect(page.findAll('tbody').map((group) => group.findAll('tr').length)).toEqual([3, 2]);
  });

  it('shows the day, the movie, the rating and the review of a viewing', async () => {
    const { page } = await openWith(ENTRIES);

    const rows = page.findAll('tbody tr:not(.has-background-light)');
    expect(rows.map((row) => row.get('td').text())).toEqual(['Sat 7', 'Sun 1', 'Sat 31']);
    expect(rows[0].findAll('td')[1].text()).toBe('Alien (1979)↻♥');
    expect(rows[0].findAll('td')[1].get('a').attributes('href')).toBe('/movies/m1');
    expect(rows[0].get('.rating-stars').text()).toBe('★★★★½');
    expect(rows[0].get('.pre-wrap').text()).toBe('Still scary.');
    expect(rows[1].findAll('td')[1].text()).toBe('Heat'); // No release date, no markers.
    expect(rows[1].find('.rating-stars').exists()).toBe(false);
    expect(rows[0].findAll('.buttons a').map((link) => link.attributes('href'))).toEqual([
      '/diary/e1/edit',
    ]);
  });

  it('says "1 entry" for one entry', async () => {
    const { page } = await openWith([ENTRIES[0]]);

    expect(text(page)).toContain('1 entry');
    expect(text(page)).not.toContain('1 entries');
  });

  it('invites to log the first viewing when the diary is empty', async () => {
    const { page } = await openWith([]);

    expect(text(page)).toContain('No diary entries yet. Log your first viewing.');
    expect(page.get('.notification a').attributes('href')).toBe('/diary/new');
    expect(page.find('table').exists()).toBe(false);
  });

  it('shows the period in the URL, and asks for it', async () => {
    const { page } = await openWith(
      [],
      '/diary?from=2026-10-01&to=2026-10-31&page=2',
      'from=2026-10-01&to=2026-10-31&page=2&pageSize=25',
    );

    expect(page.get<HTMLInputElement>('#from').element.value).toBe('2026-10-01');
    expect(page.get<HTMLInputElement>('#to').element.value).toBe('2026-10-31');
    expect(page.get('#from').attributes('max')).toBe('2026-10-31');
    expect(page.get('#to').attributes('min')).toBe('2026-10-01');
    expect(text(page)).toContain('No diary entries in this period.');
    expect(page.find('.notification a').exists()).toBe(false);
  });

  it('has nothing to clear without a period', async () => {
    const { page } = await openWith(ENTRIES);

    expect(page.findAll('button').map((button) => button.text())).not.toContain('Clear');
  });

  it('changes the period, starting again at the first page', async () => {
    const { page, router } = await openWith(ENTRIES, '/diary?page=3', 'page=3&pageSize=25');

    await page.get('#from').setValue('2026-10-01');
    await flushPromises();

    expect(router.currentRoute.value.query).toEqual({ from: '2026-10-01' });
    diaryRequest('from=2026-10-01&page=1&pageSize=25').flush(paged([ENTRIES[2]]));
    await flushPromises();

    await page.get('#to').setValue('2026-10-31');
    await flushPromises();
    expect(router.currentRoute.value.query).toEqual({ from: '2026-10-01', to: '2026-10-31' });
    diaryRequest('from=2026-10-01&to=2026-10-31&page=1&pageSize=25').flush(paged([ENTRIES[2]]));
    await flushPromises();
  });

  it('clears the period', async () => {
    const { page, router } = await openWith(
      [],
      '/diary?from=2026-10-01',
      'from=2026-10-01&page=1&pageSize=25',
    );

    await page
      .findAll('button')
      .find((button) => button.text() === 'Clear')!
      .trigger('click');
    await flushPromises();

    expect(router.currentRoute.value.fullPath).toBe('/diary');
    diaryRequest('page=1&pageSize=25').flush(paged(ENTRIES));
    await flushPromises();
    expect(text(page)).toContain('3 entries');
  });

  it('goes to another page, keeping the period', async () => {
    const opened = await open('/diary?from=2026-10-01');
    diaryRequest('from=2026-10-01&page=1&pageSize=25').flush(paged(ENTRIES, { totalCount: 60 }));
    await flushPromises();

    await opened.page.findAll('.pagination-link')[1].trigger('click');
    await flushPromises();

    expect(opened.router.currentRoute.value.query).toEqual({ from: '2026-10-01', page: '2' });
    diaryRequest('from=2026-10-01&page=2&pageSize=25').flush(
      paged([ENTRIES[2]], { page: 2, totalCount: 60 }),
    );
    await flushPromises();
  });

  it('shows why the diary cannot be loaded, until it is dismissed', async () => {
    const { page } = await open();
    diaryRequest('page=1&pageSize=25').networkError();
    await flushPromises();

    expect(page.get('[role=alert]').text()).toContain('cannot be reached');
    await page.get('button.delete').trigger('click');
    expect(page.find('[role=alert]').exists()).toBe(false);
  });

  it('deletes a viewing after the user has confirmed, and loads the diary again', async () => {
    const confirm = vi.spyOn(window, 'confirm').mockReturnValue(true);
    const { page } = await openWith(ENTRIES);

    await page.findAll('tbody tr:not(.has-background-light)')[0].get('button').trigger('click');
    expect(confirm).toHaveBeenCalledWith(
      'Delete the diary entry for "Alien" from November 7, 2026?',
    );
    const request = backend.expectOne('/api/diary-entries/e1');
    expect(request.method).toBe('DELETE');
    request.flush(null, { status: 204, statusText: 'No Content' });
    await flushPromises();

    diaryRequest('page=1&pageSize=25').flush(paged(ENTRIES.slice(1)));
    await flushPromises();
    expect(text(page)).toContain('2 entries');
  });

  it('does not delete a viewing unless the user confirms', async () => {
    vi.spyOn(window, 'confirm').mockReturnValue(false);
    const { page } = await openWith(ENTRIES);

    await page.get('tbody tr:not(.has-background-light) button').trigger('click');

    expect(text(page)).toContain('3 entries');
  });

  it('shows why a viewing could not be deleted', async () => {
    vi.spyOn(window, 'confirm').mockReturnValue(true);
    const { page } = await openWith(ENTRIES);

    await page.get('tbody tr:not(.has-background-light) button').trigger('click');
    backend
      .expectOne('/api/diary-entries/e1')
      .flush({ detail: 'Something broke.' }, { status: 500, statusText: 'Error' });
    await flushPromises();

    expect(page.get('[role=alert]').text()).toContain('Something broke.');
    expect(text(page)).toContain('3 entries'); // Still there.
  });

  it('goes to the last page when the page is gone, e.g. after deleting its only entry', async () => {
    vi.spyOn(window, 'confirm').mockReturnValue(true);
    const { page, router } = await open('/diary?page=3');
    diaryRequest('page=3&pageSize=25').flush(paged([ENTRIES[0]], { page: 3, totalCount: 51 }));
    await flushPromises();

    await page.get('tbody tr:not(.has-background-light) button').trigger('click');
    backend
      .expectOne('/api/diary-entries/e1')
      .flush(null, { status: 204, statusText: 'No Content' });
    await flushPromises();
    // 50 entries are left: two pages. The third one is empty.
    diaryRequest('page=3&pageSize=25').flush(paged([], { page: 3, totalCount: 50 }));
    await flushPromises();

    expect(router.currentRoute.value.query).toEqual({ page: '2' });
    diaryRequest('page=2&pageSize=25').flush(paged([ENTRIES[1]], { page: 2, totalCount: 50 }));
    await flushPromises();
    expect(text(page)).toContain('50 entries');
  });
});
