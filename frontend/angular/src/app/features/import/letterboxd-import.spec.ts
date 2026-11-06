import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { ImportJob } from '../../core/api/models';
import { LetterboxdImport } from './letterboxd-import';

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
  let http: HttpTestingController;
  let harness: RouterTestingHarness;

  beforeEach(async () => {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([{ path: 'import', component: LetterboxdImport }]),
        provideHttpClient(),
        provideHttpClientTesting(),
      ],
    });
    http = TestBed.inject(HttpTestingController);
    harness = await RouterTestingHarness.create();
  });

  afterEach(() => http.verify());

  /** The poll starts on a timer, so the request comes a moment after navigating. */
  const nextRequest = (url: string) => vi.waitFor(() => http.expectOne(url));

  const page = () => harness.routeNativeElement as HTMLElement;
  const text = () => page().textContent!.replace(/\s+/g, ' ');

  async function chooseFile(file: File) {
    const input = page().querySelector('input[type=file]') as HTMLInputElement;
    Object.defineProperty(input, 'files', { value: [file], configurable: true });
    input.dispatchEvent(new Event('change'));
    harness.detectChanges();
    await harness.fixture.whenStable();
  }

  const startButton = () => page().querySelector('button[type=submit]') as HTMLButtonElement;

  it('asks for the ZIP archive, and starts the import only when there is one', async () => {
    await harness.navigateByUrl('/import', LetterboxdImport);

    expect(text()).toContain('Export your data');
    expect(text()).toContain('No file chosen');
    expect(startButton().disabled).toBe(true);

    await chooseFile(new File(['zip'], 'letterboxd-export.zip'));

    expect(text()).toContain('letterboxd-export.zip');
    expect(startButton().disabled).toBe(false);
  });

  it('uploads the file and shows the progress of the import', async () => {
    await harness.navigateByUrl('/import', LetterboxdImport);
    await chooseFile(new File(['zip'], 'letterboxd-export.zip'));

    startButton().click();
    http
      .expectOne('/api/imports/letterboxd')
      .flush({ ...RUNNING, filmsTotal: 0, filmsProcessed: 0 });
    await harness.fixture.whenStable();

    expect(text()).toContain('Starting the import');
    // The ID is in the URL, so a reload shows the import again.
    expect(TestBed.inject(Router).url).toBe('/import?job=job1');
    (await nextRequest('/api/imports/letterboxd/job1')).flush(RUNNING);
    await harness.fixture.whenStable();

    expect(text()).toContain('200 of 800');
    const progress = page().querySelector('progress')!;
    expect(progress.getAttribute('value')).toBe('200');
    expect(progress.getAttribute('max')).toBe('800');
    // Polling goes on until the import has finished.
    (await nextRequest('/api/imports/letterboxd/job1')).flush(COMPLETED);
    await harness.fixture.whenStable();
    expect(text()).toContain('The import is finished');
  });

  it('shows the result of a finished import', async () => {
    await harness.navigateByUrl('/import?job=job1', LetterboxdImport);
    (await nextRequest('/api/imports/letterboxd/job1')).flush(COMPLETED);
    await harness.fixture.whenStable();

    expect(text()).toContain('Diary entries imported: 498 (2 skipped');
    expect(text()).toContain('Watchlist items imported: 314');
    expect(text()).not.toContain('on your watchlist already');
    // Films that were not imported or are only manual movies are listed by name.
    expect(text()).toContain('TMDB did not answer for 1 films');
    expect(text()).toContain('Heat (1995)');
    expect(text()).toContain('2 films could not be found on TMDB');
    expect(text()).toContain('Home Video (2019)');
    expect(text()).toContain('Obscure Short');
    expect(text()).not.toContain('Obscure Short (');
    expect(text()).toContain('diary.csv, row 7');
    expect(page().querySelector('progress')).toBeNull();
    expect(text()).toContain('Import another file');
  });

  it('shows what went wrong with a failed import', async () => {
    await harness.navigateByUrl('/import?job=job1', LetterboxdImport);
    (await nextRequest('/api/imports/letterboxd/job1')).flush({
      ...RUNNING,
      status: 'failed',
      error: 'The import failed unexpectedly.',
    });
    await harness.fixture.whenStable();

    expect(page().querySelector('.notification.is-danger')?.textContent).toContain(
      'The import failed unexpectedly.',
    );
    expect(text()).toContain('Try again');
  });

  it('explains that an import is gone and offers the upload again', async () => {
    await harness.navigateByUrl('/import?job=old', LetterboxdImport);
    (await nextRequest('/api/imports/letterboxd/old')).flush(
      { title: 'Not Found', status: 404 },
      { status: 404, statusText: 'Not Found' },
    );
    await harness.fixture.whenStable();

    expect(text()).toContain('not available any more');
    expect(page().querySelector('input[type=file]')).not.toBeNull();
  });

  it('shows the error of a rejected upload and keeps the chosen file', async () => {
    await harness.navigateByUrl('/import', LetterboxdImport);
    await chooseFile(new File(['x'], 'wrong.zip'));

    startButton().click();
    http.expectOne('/api/imports/letterboxd').flush(
      {
        title: 'One or more validation errors occurred.',
        status: 400,
        errors: { file: ['The ZIP archive has neither diary.csv nor watchlist.csv.'] },
      },
      { status: 400, statusText: 'Bad Request' },
    );
    await harness.fixture.whenStable();

    expect(page().querySelector('.notification.is-danger')?.textContent).toContain(
      'neither diary.csv nor watchlist.csv',
    );
    expect(text()).toContain('wrong.zip');
    expect(startButton().disabled).toBe(false);
  });
});
