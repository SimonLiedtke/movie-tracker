import { flushPromises, type VueWrapper } from '@vue/test-utils';
import { describe, expect, it } from 'vitest';
import { createApi } from '../../core/api/api';
import { createHttp } from '../../core/api/http';
import type { ActivityLogEntry, PagedResponse } from '../../core/api/models';
import { activityLogProps } from '../../route-props';
import { useFakeBackend } from '../../test/fake-backend';
import { mountRoute } from '../../test/services';
import ActivityLog from './ActivityLog.vue';

const entry = (changes: Partial<ActivityLogEntry>): ActivityLogEntry => ({
  id: 'a1',
  type: 'movieAdded',
  occurredAt: '2026-11-05T21:30:00Z',
  movieId: null,
  movieTitle: null,
  detail: null,
  ...changes,
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
const ENTRIES = [VIEWING, NOTE, DELETED, IMPORT];

const paged = (
  items: ActivityLogEntry[],
  { page = 1, totalCount = items.length } = {},
): PagedResponse<ActivityLogEntry> => ({ items, page, pageSize: 50, totalCount });

describe('ActivityLog', () => {
  const backend = useFakeBackend();

  async function open(url = '/activity') {
    return mountRoute(
      url,
      { path: '/activity', component: ActivityLog, props: activityLogProps },
      { api: createApi(createHttp()) },
    );
  }

  const logRequest = (query: string) => backend.expectOne(`/api/activity-log?${query}`);
  const text = (page: VueWrapper) => page.text().replace(/\s+/g, ' ');
  const rows = (page: VueWrapper) => page.findAll('tbody tr');

  async function openWith(
    entries: ActivityLogEntry[],
    url = '/activity',
    query = 'page=1&pageSize=50',
  ) {
    const opened = await open(url);
    logRequest(query).flush(paged(entries));
    await flushPromises();
    return opened;
  }

  it('shows a progress bar until the log is loaded', async () => {
    const { page } = await open();
    expect(page.find('progress').exists()).toBe(true);

    logRequest('page=1&pageSize=50').flush(paged(ENTRIES));
    await flushPromises();

    expect(page.find('progress').exists()).toBe(false);
    expect(text(page)).toContain('4 actions');
  });

  it('lists the actions with their time, what was done, the movie and the details', async () => {
    const { page } = await openWith(ENTRIES);

    expect(rows(page)).toHaveLength(4);
    const first = rows(page)[0];
    expect(first.get('time').attributes('datetime')).toBe('2026-11-07T20:15:00Z');
    expect(first.get('time').text()).toContain('2026');
    expect(first.text()).toContain('Logged a viewing');
    expect(first.text()).toContain('Alien');
    expect(first.text()).toContain('Watched on 2026-11-07 · 4.5 stars');
    expect(rows(page)[1].text()).toContain('Changed a watchlist note');
    expect(rows(page)[1].text()).toContain('Note: with Sam');
  });

  it('links to a movie that still exists, and only names one that was deleted', async () => {
    const { page } = await openWith(ENTRIES);

    expect(rows(page)[0].get('a').attributes('href')).toBe('/movies/m1');
    expect(rows(page)[2].find('a').exists()).toBe(false);
    expect(rows(page)[2].text()).toContain('Deleted a movie');
    expect(rows(page)[2].text()).toContain('Home Video');
  });

  it('shows an action that is not about a movie without one', async () => {
    const { page } = await openWith(ENTRIES);

    expect(rows(page)[3].text()).toContain('Imported from Letterboxd');
    expect(rows(page)[3].find('a').exists()).toBe(false);
  });

  it('says so when nothing was done yet', async () => {
    const { page } = await openWith([]);

    expect(text(page)).toContain('Nothing yet');
    expect(rows(page)).toHaveLength(0);
  });

  it('shows the requested page and goes to another one with the pagination', async () => {
    const opened = await open('/activity?page=2');
    logRequest('page=2&pageSize=50').flush(paged([VIEWING], { page: 2, totalCount: 120 }));
    await flushPromises();
    expect(text(opened.page)).toContain('120 actions');

    await opened.page.get('.pagination-next').trigger('click');
    await flushPromises();

    expect(opened.router.currentRoute.value.query).toEqual({ page: '3' });
    logRequest('page=3&pageSize=50').flush(paged([NOTE], { page: 3, totalCount: 120 }));
    await flushPromises();
    expect(rows(opened.page)[0].text()).toContain('Changed a watchlist note');
  });

  it('leaves the page out of the URL on the first page', async () => {
    const opened = await open('/activity?page=2');
    logRequest('page=2&pageSize=50').flush(paged([VIEWING], { page: 2, totalCount: 120 }));
    await flushPromises();

    await opened.page.get('.pagination-previous').trigger('click');
    await flushPromises();

    expect(opened.router.currentRoute.value.query).toEqual({});
    logRequest('page=1&pageSize=50').flush(paged([VIEWING], { totalCount: 120 }));
    await flushPromises();
  });

  it('goes to the last page if the requested one is past the end', async () => {
    const { page, router } = await open('/activity?page=9');
    logRequest('page=9&pageSize=50').flush(paged([], { page: 9, totalCount: 120 }));
    await flushPromises();

    expect(router.currentRoute.value.query).toEqual({ page: '3' });
    logRequest('page=3&pageSize=50').flush(paged([NOTE], { page: 3, totalCount: 120 }));
    await flushPromises();
    expect(rows(page)).toHaveLength(1);
  });

  it('shows why the log cannot be loaded, until it is dismissed', async () => {
    const { page } = await open();
    logRequest('page=1&pageSize=50').networkError();
    await flushPromises();

    expect(page.get('[role=alert]').text()).toContain('cannot be reached');
    await page.get('button.delete').trigger('click');
    expect(page.find('[role=alert]').exists()).toBe(false);
  });
});
