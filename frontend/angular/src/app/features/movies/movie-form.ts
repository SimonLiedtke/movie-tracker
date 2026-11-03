import { Component, effect, inject, input, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { catchError, of } from 'rxjs';
import { GenresApi, MoviesApi } from '../../core/api/api';
import { ApiError, toApiError } from '../../core/api/api-error';
import { Movie, MovieRequest } from '../../core/api/models';
import { ErrorNotification } from '../../shared/error-notification';

/** Empty strings become null, the backend expects absent values as null. */
const orNull = <T>(value: T | '' | null): T | null => (value === '' ? null : value);

@Component({
  selector: 'app-movie-form',
  imports: [ReactiveFormsModule, RouterLink, ErrorNotification],
  templateUrl: './movie-form.html',
})
export class MovieForm {
  private readonly router = inject(Router);
  private readonly moviesApi = inject(MoviesApi);

  /** Route parameter; not set when adding a movie. */
  readonly id = input<string>();

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
    tmdbId: [null as number | null, Validators.min(1)],
    genreIds: [[] as number[]],
  });

  constructor() {
    effect(() => {
      const id = this.id();
      if (id) {
        this.load(id);
      }
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
      tmdbId: value.tmdbId,
      genreIds: value.genreIds,
    };

    const id = this.id();
    this.saving.set(true);
    this.error.set(null);
    (id ? this.moviesApi.update(id, request) : this.moviesApi.create(request)).subscribe({
      next: (movie) => this.router.navigate(['/movies', movie.id]),
      error: (error) => {
        this.saving.set(false);
        this.error.set(toApiError(error));
      },
    });
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
    tmdbId: movie.tmdbId,
    genreIds: movie.genres.map((genre) => genre.id),
  };
}
