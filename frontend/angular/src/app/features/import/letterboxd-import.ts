import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import {
  EMPTY,
  Observable,
  catchError,
  distinctUntilChanged,
  exhaustMap,
  map,
  switchMap,
  takeWhile,
  tap,
  timer,
} from 'rxjs';
import { LetterboxdImportApi } from '../../core/api/api';
import { ApiError, toApiError } from '../../core/api/api-error';
import { ImportFilm, ImportJob } from '../../core/api/models';
import { ErrorNotification } from '../../shared/error-notification';

const POLL_INTERVAL_MS = 1000;

/**
 * Uploads a Letterboxd export. The server imports it in the background, which takes a while for a big
 * diary (every film is looked up on TMDB), so this page shows the progress. The import's ID is in the URL
 * (?job=...): reloading the page, or coming back later, shows the progress or result again.
 */
@Component({
  selector: 'app-letterboxd-import',
  imports: [RouterLink, ErrorNotification],
  templateUrl: './letterboxd-import.html',
})
export class LetterboxdImport {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly api = inject(LetterboxdImportApi);

  protected readonly file = signal<File | null>(null);
  protected readonly uploading = signal(false);
  protected readonly job = signal<ImportJob | null>(null);
  protected readonly error = signal<ApiError | null>(null);
  /** The server does not know the import in the URL (any more), e.g. because it was restarted. */
  protected readonly lost = signal(false);

  constructor() {
    this.route.queryParamMap
      .pipe(
        map((params) => params.get('job')),
        distinctUntilChanged(),
        tap((id) => {
          this.lost.set(false);
          if (!id) {
            this.job.set(null);
          }
        }),
        switchMap((id) => (id ? this.poll(id) : EMPTY)),
        takeUntilDestroyed(),
      )
      .subscribe((job) => this.job.set(job));
  }

  protected chooseFile(event: Event): void {
    this.file.set((event.target as HTMLInputElement).files?.[0] ?? null);
    this.error.set(null);
  }

  protected start(): void {
    const file = this.file();
    if (!file || this.uploading()) {
      return;
    }

    this.uploading.set(true);
    this.error.set(null);
    this.api.start(file).subscribe({
      next: (job) => {
        this.uploading.set(false);
        this.job.set(job);
        this.router.navigate([], { queryParams: { job: job.id } });
      },
      error: (error) => {
        this.uploading.set(false);
        this.error.set(toApiError(error));
      },
    });
  }

  /** Back to the upload form. */
  protected reset(): void {
    this.file.set(null);
    this.error.set(null);
    this.router.navigate([], { queryParams: {} });
  }

  protected filmLabel(film: ImportFilm): string {
    return film.year ? `${film.name} (${film.year})` : film.name;
  }

  /** Asks for the job until it has finished. */
  private poll(id: string): Observable<ImportJob> {
    return timer(0, POLL_INTERVAL_MS).pipe(
      // Never two requests at once, even if the server is slow.
      exhaustMap(() => this.api.get(id)),
      takeWhile((job) => job.status === 'running', true),
      catchError((error) => {
        if (error instanceof HttpErrorResponse && error.status === 404) {
          this.lost.set(true);
        } else {
          this.error.set(toApiError(error));
        }
        return EMPTY;
      }),
    );
  }
}
