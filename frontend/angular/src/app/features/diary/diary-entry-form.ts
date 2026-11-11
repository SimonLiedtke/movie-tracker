import { Component, effect, inject, input, signal, untracked } from '@angular/core';
import {
  AbstractControl,
  NonNullableFormBuilder,
  ReactiveFormsModule,
  ValidationErrors,
  Validators,
} from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { DiaryApi, MoviesApi } from '../../core/api/api';
import { ApiError, toApiError } from '../../core/api/api-error';
import { DiaryEntryRequest, MovieRef } from '../../core/api/models';
import { todayIso } from '../../shared/dates';
import { ErrorNotification } from '../../shared/error-notification';
import { MoviePicker } from '../../shared/movie-picker';
import { RATING_OPTIONS } from '../../shared/rating';
import { MAX_TAGS, MAX_TAG_LENGTH, formatTags, parseTags, tagsError } from '../../shared/tags';

/** The text of the tags field must have valid tags, see tagsError. */
function tagsValidator(control: AbstractControl<string>): ValidationErrors | null {
  const error = tagsError(control.value);
  return error ? { tags: error } : null;
}

@Component({
  selector: 'app-diary-entry-form',
  imports: [ReactiveFormsModule, RouterLink, ErrorNotification, MoviePicker],
  templateUrl: './diary-entry-form.html',
})
export class DiaryEntryForm {
  private readonly router = inject(Router);
  private readonly diaryApi = inject(DiaryApi);
  private readonly moviesApi = inject(MoviesApi);

  /** Route parameter; not set when logging a new viewing. */
  readonly id = input<string>();
  /** Query parameter to preselect the movie, e.g. when coming from a movie page. */
  readonly movieId = input<string>();

  protected readonly today = todayIso();
  protected readonly ratingOptions = RATING_OPTIONS;
  protected readonly maxTags = MAX_TAGS;
  protected readonly maxTagLength = MAX_TAG_LENGTH;
  protected readonly movie = signal<MovieRef | null>(null);
  protected readonly movieTouched = signal(false);
  protected readonly loading = signal(false);
  protected readonly saving = signal(false);
  protected readonly error = signal<ApiError | null>(null);

  protected readonly form = inject(NonNullableFormBuilder).group({
    watchedOn: [this.today, Validators.required],
    rating: [null as number | null],
    review: ['', Validators.maxLength(20000)],
    isRewatch: [false],
    liked: [false],
    tags: ['', tagsValidator],
  });

  constructor() {
    effect(() => {
      const id = this.id();
      const movieId = this.movieId();
      untracked(() => {
        if (id) {
          this.loadEntry(id);
        } else if (movieId) {
          this.preselectMovie(movieId);
        }
      });
    });
  }

  protected isInvalid(name: keyof typeof this.form.controls): boolean {
    const control = this.form.controls[name];
    return control.invalid && (control.touched || control.dirty);
  }

  protected save(): void {
    const movie = this.movie();
    if (this.form.invalid || !movie) {
      this.form.markAllAsTouched();
      this.movieTouched.set(true);
      return;
    }

    const value = this.form.getRawValue();
    const request: DiaryEntryRequest = {
      movieId: movie.id,
      watchedOn: value.watchedOn,
      rating: value.rating,
      review: value.review.trim() || null,
      isRewatch: value.isRewatch,
      liked: value.liked,
      tags: parseTags(value.tags),
    };

    const id = this.id();
    this.saving.set(true);
    this.error.set(null);
    (id ? this.diaryApi.update(id, request) : this.diaryApi.create(request)).subscribe({
      next: (entry) => this.router.navigate(['/movies', entry.movie.id]),
      error: (error) => {
        this.saving.set(false);
        this.error.set(toApiError(error));
      },
    });
  }

  protected cancelLink(): unknown[] {
    const movie = this.movie();
    return this.movieId() && movie ? ['/movies', movie.id] : ['/diary'];
  }

  private loadEntry(id: string): void {
    this.loading.set(true);
    this.diaryApi.get(id).subscribe({
      next: (entry) => {
        this.movie.set(entry.movie);
        this.form.setValue({
          watchedOn: entry.watchedOn,
          rating: entry.rating,
          review: entry.review ?? '',
          isRewatch: entry.isRewatch,
          liked: entry.liked,
          tags: formatTags(entry.tags),
        });
        this.loading.set(false);
      },
      error: (error) => {
        this.error.set(toApiError(error));
        this.loading.set(false);
      },
    });
  }

  private preselectMovie(movieId: string): void {
    this.moviesApi.get(movieId).subscribe({
      next: (movie) => {
        this.movie.set(movie);
        // Watched before: most likely a rewatch.
        if (movie.watchCount > 0) {
          this.form.controls.isRewatch.setValue(true);
        }
      },
      error: (error) => this.error.set(toApiError(error)),
    });
  }
}
