import { flushPromises, type VueWrapper } from '@vue/test-utils';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { createApi } from '../../core/api/api';
import { createHttp } from '../../core/api/http';
import type { ImportJob } from '../../core/api/models';
import { letterboxdImportProps } from '../../route-props';
import { useFakeBackend } from '../../test/fake-backend';
import { mountRoute } from '../../test/services';
import LetterboxdImport from './LetterboxdImport.vue';

const RUNNING: ImportJob = {
  id: 'job1',
  status: 'running',
  filmsTotal: 800,
  filmsProcessed: 200,
  result: null,
  error: null,
};

const COMPLETED: ImportJob = {
  ...RUNNING,
  status: 'completed',
  filmsProcessed: 800,
  result: {
    diaryEntriesImported: 498,
    diaryEntriesSkipped: 2,
    watchlistItemsImported: 314,
    watchlistItemsSkipped: 0,
    manualMovies: [
      { name: 'Home Video', year: 2019 },
      { name: 'Obscure Short', year: null },
    ],
    failedFilms: [{ name: 'Heat', year: 1995 }],
    warnings: ['diary.csv, row 7: skipped, the film has no name.'],
  },
};

describe('LetterboxdImport', () => {
  const backend = useFakeBackend();

  afterEach(() => vi.useRealTimers());

  const open = (url = '/import') =>
    mountRoute(
      url,
      { path: '/import', component: LetterboxdImport, props: letterboxdImportProps },
      { api: createApi(createHttp()) },
    );

  const jobRequest = (id = 'job1') => backend.expectOne(`/api/imports/letterboxd/${id}`);
  const text = (page: VueWrapper) => page.text().replace(/\s+/g, ' ');
  const startButton = (page: VueWrapper) => page.get<HTMLButtonElement>('button[type=submit]');

  async function chooseFile(page: VueWrapper, file: File) {
    const input = page.get<HTMLInputElement>('input[type=file]');
    Object.defineProperty(input.element, 'files', { value: [file], configurable: true });
    await input.trigger('change');
  }

  it('asks for the ZIP archive, and starts the import only when there is one', async () => {
    const { page } = await open();

    expect(text(page)).toContain('Export your data');
    expect(text(page)).toContain('No file chosen');
    expect(startButton(page).element.disabled).toBe(true);

    await chooseFile(page, new File(['zip'], 'letterboxd-export.zip'));

    expect(text(page)).toContain('letterboxd-export.zip');
    expect(startButton(page).element.disabled).toBe(false);
  });

  it('uploads the file and shows the progress of the import', async () => {
    vi.useFakeTimers({ toFake: ['setTimeout', 'clearTimeout'] });
    const { page, router } = await open();
    await chooseFile(page, new File(['zip'], 'letterboxd-export.zip'));

    await page.get('form').trigger('submit');
    expect(startButton(page).classes()).toContain('is-loading');
    backend
      .expectOne('/api/imports/letterboxd')
      .flush({ ...RUNNING, filmsTotal: 0, filmsProcessed: 0 });
    await flushPromises();

    expect(text(page)).toContain('Starting the import');
    expect(page.get('progress').attributes('value')).toBeUndefined(); // Indeterminate.
    // The ID is in the URL, so a reload shows the import again.
    expect(router.currentRoute.value.fullPath).toBe('/import?job=job1');
    jobRequest().flush(RUNNING);
    await flushPromises();

    expect(text(page)).toContain('200 of 800');
    const progress = page.get('progress');
    expect(progress.attributes('value')).toBe('200');
    expect(progress.attributes('max')).toBe('800');

    // Polling goes on, once a second, until the import has finished.
    await vi.advanceTimersByTimeAsync(999);
    backend.verify();
    await vi.advanceTimersByTimeAsync(1);
    jobRequest().flush({ ...RUNNING, filmsProcessed: 500 });
    await flushPromises();
    expect(text(page)).toContain('500 of 800');

    await vi.advanceTimersByTimeAsync(1000);
    jobRequest().flush(COMPLETED);
    await flushPromises();
    expect(text(page)).toContain('The import is finished');

    await vi.advanceTimersByTimeAsync(5000);
    backend.verify(); // No more polling.
  });

  it('shows the result of a finished import', async () => {
    const { page } = await open('/import?job=job1');
    jobRequest().flush(COMPLETED);
    await flushPromises();

    expect(text(page)).toContain('Diary entries imported: 498 (2 skipped');
    expect(text(page)).toContain('Watchlist items imported: 314');
    expect(text(page)).not.toContain('on your watchlist already');
    // Films that were not imported or are only manual movies are listed by name.
    expect(text(page)).toContain('TMDB did not answer for 1 films');
    expect(text(page)).toContain('Heat (1995)');
    expect(text(page)).toContain('2 films could not be found on TMDB');
    expect(text(page)).toContain('Home Video (2019)');
    expect(text(page)).toContain('Obscure Short');
    expect(text(page)).not.toContain('Obscure Short (');
    expect(text(page)).toContain('diary.csv, row 7');
    expect(page.find('progress').exists()).toBe(false);
    expect(text(page)).toContain('Import another file');
    expect(page.findAll('.buttons a').map((link) => link.attributes('href'))).toEqual([
      '/diary',
      '/watchlist',
    ]);
    expect(page.get('details a').attributes('href')).toBe('/movies');
  });

  it('leaves out what there is nothing to say about', async () => {
    const { page } = await open('/import?job=job1');
    jobRequest().flush({
      ...COMPLETED,
      result: {
        diaryEntriesImported: 10,
        diaryEntriesSkipped: 0,
        watchlistItemsImported: 5,
        watchlistItemsSkipped: 3,
        manualMovies: [],
        failedFilms: [],
        warnings: [],
      },
    });
    await flushPromises();

    expect(text(page)).toContain('Diary entries imported: 10');
    expect(text(page)).not.toContain('in your diary already');
    expect(text(page)).toContain(
      'Watchlist items imported: 5 (3 skipped, they were on your watchlist already)',
    );
    expect(page.find('.is-danger').exists()).toBe(false);
    expect(page.find('details').exists()).toBe(false);
  });

  it('shows what went wrong with a failed import', async () => {
    const { page } = await open('/import?job=job1');
    jobRequest().flush({ ...RUNNING, status: 'failed', error: 'The import failed unexpectedly.' });
    await flushPromises();

    expect(page.get('.notification.is-danger').text()).toContain('The import failed unexpectedly.');
    expect(text(page)).toContain('Try again');
  });

  it('explains that an import is gone and offers the upload again', async () => {
    const { page } = await open('/import?job=old');
    jobRequest('old').flush(
      { title: 'Not Found', status: 404 },
      { status: 404, statusText: 'Not Found' },
    );
    await flushPromises();

    expect(text(page)).toContain('not available any more');
    expect(page.find('input[type=file]').exists()).toBe(true);
  });

  it('shows why an import cannot be followed, and stops asking', async () => {
    vi.useFakeTimers({ toFake: ['setTimeout', 'clearTimeout'] });
    const { page } = await open('/import?job=job1');
    jobRequest().flush({ title: 'Oops' }, { status: 500, statusText: 'Error' });
    await flushPromises();

    expect(page.get('[role=alert]').text()).toContain('Oops');
    await vi.advanceTimersByTimeAsync(5000);
    backend.verify();
  });

  it('shows the error of a rejected upload and keeps the chosen file', async () => {
    const { page } = await open();
    await chooseFile(page, new File(['x'], 'wrong.zip'));

    await page.get('form').trigger('submit');
    backend.expectOne('/api/imports/letterboxd').flush(
      {
        title: 'One or more validation errors occurred.',
        status: 400,
        errors: { file: ['The ZIP archive has neither diary.csv nor watchlist.csv.'] },
      },
      { status: 400, statusText: 'Bad Request' },
    );
    await flushPromises();

    expect(page.get('.notification.is-danger').text()).toContain(
      'neither diary.csv nor watchlist.csv',
    );
    expect(text(page)).toContain('wrong.zip');
    expect(startButton(page).element.disabled).toBe(false);
  });

  it('does not upload twice', async () => {
    const { page } = await open();
    await chooseFile(page, new File(['zip'], 'letterboxd-export.zip'));

    await page.get('form').trigger('submit');
    await page.get('form').trigger('submit');

    expect(startButton(page).element.disabled).toBe(true);
    backend.expectOne('/api/imports/letterboxd').flush(RUNNING);
    await flushPromises();
    jobRequest().flush(COMPLETED);
    await flushPromises();
  });

  it('goes back to the upload form for another file', async () => {
    const { page, router } = await open('/import?job=job1');
    jobRequest().flush(COMPLETED);
    await flushPromises();

    await page.get('button.button').trigger('click');
    await flushPromises();

    expect(router.currentRoute.value.fullPath).toBe('/import');
    expect(text(page)).toContain('No file chosen');
    expect(page.find('progress').exists()).toBe(false);
  });

  it('follows another import when the URL changes', async () => {
    const { page, router } = await open('/import?job=job1');
    jobRequest().flush(COMPLETED);
    await flushPromises();

    await router.push('/import?job=job2');
    jobRequest('job2').flush({ ...RUNNING, id: 'job2', filmsProcessed: 10 });
    await flushPromises();

    expect(text(page)).toContain('10 of 800');
  });

  it('stops asking when the user leaves the page', async () => {
    vi.useFakeTimers({ toFake: ['setTimeout', 'clearTimeout'] });
    const { page, router } = await open('/import?job=job1');
    jobRequest().flush(RUNNING);
    await flushPromises();

    await router.push('/movies');
    await vi.advanceTimersByTimeAsync(5000);

    backend.verify();
    expect(page.find('progress').exists()).toBe(false);
  });
});
