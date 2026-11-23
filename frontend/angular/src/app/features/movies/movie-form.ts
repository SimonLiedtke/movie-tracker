import { Component, computed, effect, inject, input, signal, untracked } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { Observable, catchError, map, of, switchMap, tap } from 'rxjs';
import { GenresApi, MoviesApi, WatchlistApi } from '../../core/api/api';
import { ApiError, toApiError } from '../../core/api/api-error';
import { Movie, MovieRequest } from '../../core/api/models';
import { ErrorNotification } from '../../shared/error-notification';
import { AfterManualAdd } from '../../shared/movie-picker';
import { Poster } from '../../shared/poster';

interface Navigation {
  path: unknown[];
  queryParams?: Record<string, string>;
}

const POSTER_URL_PATTERN = /^https?:\/\/\S+$/;
const POSTER_URL_MAX_LENGTH = 2000;

/** What the server accepts as uploaded poster, see `PUT /api/movies/{movieId}/poster`. */
const POSTER_TYPES = ['image/jpeg', 'image/png', 'image/webp'];
const POSTER_MAX_BYTES = 5 * 1024 * 1024;

/** Empty strings become null, the backend expects absent values as null. */
const orNull = <T>(value: T | '' | null): T | null => (value === '' ? null : value);

/**
 * Adds or edits a manually added movie: one that TMDB does not know, or that the user wants to describe
 * themselves. Only its owner sees it. Movies from TMDB cannot be edited.
 */
@Component({
  selector: 'app-movie-form',
  imports: [ReactiveFormsModule, RouterLink, ErrorNotification, Poster],
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
    posterUrl: [
      '',
      [Validators.maxLength(POSTER_URL_MAX_LENGTH), Validators.pattern(POSTER_URL_PATTERN)],
    ],
    genreIds: [[] as number[]],
  });

  protected readonly formValue = toSignal(this.form.valueChanges, {
    initialValue: this.form.getRawValue(),
  });

  /** The image file the user has chosen as poster; it is uploaded when the movie is saved. */
  protected readonly posterFile = signal<File | null>(null);
  /** The uploaded poster the movie already has (editing only), as long as the user does not remove or replace it. */
  protected readonly uploadedPosterUrl = signal<string | null>(null);
  /** The movie that was added, if saving failed after that (e.g. the poster upload): saving again changes it. */
  private readonly addedId = signal<string | null>(null);

  /** The chosen file is not an image the server accepts. */
  protected readonly posterFileInvalid = computed(() => {
    const file = this.posterFile();
    return file !== null && (!POSTER_TYPES.includes(file.type) || file.size > POSTER_MAX_BYTES);
  });

  /** A preview of the chosen file. The browser holds on to it until it is revoked. */
  private readonly chosenPosterUrl = signal<string | null>(null);

  /** The poster the movie will have after saving, as far as the form can tell. */
  protected readonly posterPreviewUrl = computed(() => {
    const chosen = this.chosenPosterUrl();
    if (chosen) {
      return chosen;
    }
    const typed = this.formValue().posterUrl?.trim();
    if (typed) {
      return POSTER_URL_PATTERN.test(typed) && typed.length <= POSTER_URL_MAX_LENGTH ? typed : null;
    }
    return this.uploadedPosterUrl();
  });

  constructor() {
    effect((onCleanup) => {
      const file = this.posterFileInvalid() ? null : this.posterFile();
      if (!file) {
        this.chosenPosterUrl.set(null);
        return;
      }
      const url = URL.createObjectURL(file);
      this.chosenPosterUrl.set(url);
      onCleanup(() => URL.revokeObjectURL(url));
    });

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

  protected onPosterFile(event: Event): void {
    this.posterFile.set((event.target as HTMLInputElement).files?.[0] ?? null);
  }

  /** Forgets the chosen file, or: removes the uploaded poster when the movie is saved. */
  protected removePoster(): void {
    if (this.posterFile()) {
      this.posterFile.set(null);
    } else {
      this.uploadedPosterUrl.set(null);
    }
  }

  protected save(): void {
    if (this.form.invalid || this.posterFileInvalid()) {
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
      // A typed URL replaces an uploaded poster. Without one, the movie keeps its uploaded poster (by sending
      // its URL back) unless the user removed it.
      posterUrl: orNull(value.posterUrl.trim()) ?? this.uploadedPosterUrl(),
      genreIds: value.genreIds,
    };

    this.saving.set(true);
    this.error.set(null);
    this.saveMovie(request).subscribe({
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

  /** Saves the movie and its poster (a second request), then returns where to continue. */
  private saveMovie(request: MovieRequest): Observable<Navigation> {
    const id = this.id();
    const existingId = id ?? this.addedId();
    const movie$ = existingId
      ? this.moviesApi.update(existingId, request)
      : this.moviesApi.create(request).pipe(tap((movie) => this.addedId.set(movie.id)));
    const file = this.posterFile();

    return movie$.pipe(
      switchMap((movie) =>
        file ? this.moviesApi.uploadPoster(movie.id, file).pipe(map(() => movie)) : of(movie),
      ),
      switchMap((movie) =>
        id ? of<Navigation>({ path: ['/movies', movie.id] }) : this.continueAfterAdding(movie.id),
      ),
    );
  }

  /** Continues where the user came from. */
  private continueAfterAdding(movieId: string): Observable<Navigation> {
    switch (this.next()) {
      case 'diary':
        return of({ path: ['/diary/new'], queryParams: { movieId } });
      case 'watchlist':
        return this.watchlistApi.put(movieId, null).pipe(map(() => ({ path: ['/watchlist'] })));
      default:
        return of({ path: ['/movies', movieId] });
    }
  }

  private load(id: string): void {
    this.loading.set(true);
    this.moviesApi.get(id).subscribe({
      next: (movie) => {
        // An uploaded poster has a path on this server as URL; it is not something to edit in the URL field.
        const uploaded = movie.posterUrl?.startsWith('/') ?? false;
        this.uploadedPosterUrl.set(uploaded ? movie.posterUrl : null);
        this.form.setValue(toFormValue(movie, uploaded));
        this.loading.set(false);
      },
      error: (error) => {
        this.error.set(toApiError(error));
        this.loading.set(false);
      },
    });
  }
}

function toFormValue(movie: Movie, hasUploadedPoster: boolean) {
  return {
    title: movie.title,
    originalTitle: movie.originalTitle ?? '',
    releaseDate: movie.releaseDate ?? '',
    runtimeMinutes: movie.runtimeMinutes,
    overview: movie.overview ?? '',
    posterUrl: hasUploadedPoster ? '' : (movie.posterUrl ?? ''),
    genreIds: movie.genres.map((genre) => genre.id),
  };
}
