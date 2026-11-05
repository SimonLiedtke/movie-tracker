import { Component, effect, inject, input, signal, untracked } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { Observable, catchError, map, of, switchMap } from 'rxjs';
import { GenresApi, MoviesApi, WatchlistApi } from '../../core/api/api';
import { ApiError, toApiError } from '../../core/api/api-error';
import { Movie, MovieRequest } from '../../core/api/models';
import { ErrorNotification } from '../../shared/error-notification';
import { AfterManualAdd } from '../../shared/movie-picker';

interface Navigation {
  path: unknown[];
  queryParams?: Record<string, string>;
}

/** Empty strings become null, the backend expects absent values as null. */
const orNull = <T>(value: T | '' | null): T | null => (value === '' ? null : value);

/**
 * Adds or edits a manually added movie: one that TMDB does not know, or that the user wants to describe
 * themselves. Only its owner sees it. Movies from TMDB cannot be edited.
 */
@Component({
  selector: 'app-movie-form',
  imports: [ReactiveFormsModule, RouterLink, ErrorNotification],
  templateUrl: './movie-form.html',
})
export class MovieForm {
  private readonly router = inject(Router);
  private readonly moviesApi = inject(MoviesApi);
  private readonly watchlistApi = inject(WatchlistApi);

  /** Route parameter; not set when adding a movie. */
  readonly id = input<string>();
  /** Query parameter: title to start with, e.g. what the user searched for. */
  readonly prefillTitle = input<string>(undefined, { alias: 'title' });
  /** Query parameter: where to continue after adding, e.g. back to logging a viewing. */
  readonly next = input<AfterManualAdd>();

  protected readonly genres = toSignal(
    inject(GenresApi)
      .list()
      .pipe(catchError(() => of([]))),
    {
      initialValue: [],
    },
  );
  protected readonly loading = signal(false);
  protected readonly saving = signal(false);
  protected readonly error = signal<ApiError | null>(null);

  protected readonly form = inject(NonNullableFormBuilder).group({
    title: ['', [Validators.required, Validators.maxLength(300)]],
    originalTitle: ['', Validators.maxLength(300)],
    releaseDate: [''],
    runtimeMinutes: [null as number | null, [Validators.min(1), Validators.max(1500)]],
    overview: ['', Validators.maxLength(5000)],
    posterUrl: ['', [Validators.maxLength(2000), Validators.pattern(/^https?:\/\/\S+$/)]],
    genreIds: [[] as number[]],
  });

  constructor() {
    effect(() => {
      const id = this.id();
      const title = this.prefillTitle();
      untracked(() => {
        if (id) {
          this.load(id);
        } else if (title) {
          this.form.controls.title.setValue(title);
        }
      });
    });
  }

  protected hasGenre(genreId: number): boolean {
    return this.form.controls.genreIds.value.includes(genreId);
  }

  protected toggleGenre(genreId: number, checked: boolean): void {
    const current = this.form.controls.genreIds.value;
    this.form.controls.genreIds.setValue(
      checked ? [...current, genreId] : current.filter((id) => id !== genreId),
    );
  }

  protected isInvalid(name: keyof typeof this.form.controls): boolean {
    const control = this.form.controls[name];
    return control.invalid && (control.touched || control.dirty);
  }

  protected save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const value = this.form.getRawValue();
    const request: MovieRequest = {
      title: value.title.trim(),
      originalTitle: orNull(value.originalTitle.trim()),
      releaseDate: orNull(value.releaseDate),
      runtimeMinutes: value.runtimeMinutes,
      overview: orNull(value.overview.trim()),
      posterUrl: orNull(value.posterUrl.trim()),
      genreIds: value.genreIds,
    };

    const id = this.id();
    this.saving.set(true);
    this.error.set(null);
    const save$: Observable<Navigation> = id
      ? this.moviesApi.update(id, request).pipe(map((movie) => ({ path: ['/movies', movie.id] })))
      : this.addAndContinue(request);
    save$.subscribe({
      next: (commands) =>
        this.router.navigate(commands.path, { queryParams: commands.queryParams }),
      error: (error) => {
        this.saving.set(false);
        this.error.set(toApiError(error));
      },
    });
  }

  protected cancelLink(): unknown[] {
    const id = this.id();
    if (id) {
      return ['/movies', id];
    }
    return [
      this.next() === 'diary'
        ? '/diary/new'
        : this.next() === 'watchlist'
          ? '/watchlist'
          : '/movies',
    ];
  }

  /** Creates the movie, then continues where the user came from. */
  private addAndContinue(request: MovieRequest): Observable<Navigation> {
    return this.moviesApi.create(request).pipe(
      switchMap((movie): Observable<Navigation> => {
        switch (this.next()) {
          case 'diary':
            return of({ path: ['/diary/new'], queryParams: { movieId: movie.id } });
          case 'watchlist':
            return this.watchlistApi
              .put(movie.id, null)
              .pipe(map(() => ({ path: ['/watchlist'] })));
          default:
            return of({ path: ['/movies', movie.id] });
        }
      }),
    );
  }

  private load(id: string): void {
    this.loading.set(true);
    this.moviesApi.get(id).subscribe({
      next: (movie) => {
        this.form.setValue(toFormValue(movie));
        this.loading.set(false);
      },
      error: (error) => {
        this.error.set(toApiError(error));
        this.loading.set(false);
      },
    });
  }
}

function toFormValue(movie: Movie) {
  return {
    title: movie.title,
    originalTitle: movie.originalTitle ?? '',
    releaseDate: movie.releaseDate ?? '',
    runtimeMinutes: movie.runtimeMinutes,
    overview: movie.overview ?? '',
    posterUrl: movie.posterUrl ?? '',
    genreIds: movie.genres.map((genre) => genre.id),
  };
}
