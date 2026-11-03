import { Component, effect, inject, input, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { Observable, forkJoin } from 'rxjs';
import { DiaryApi, MoviesApi, WatchlistApi } from '../../core/api/api';
import { ApiError, toApiError } from '../../core/api/api-error';
import { DiaryEntry, Movie } from '../../core/api/models';
import { yearOf } from '../../shared/dates';
import { ErrorNotification } from '../../shared/error-notification';
import { Poster } from '../../shared/poster';
import { Rating } from '../../shared/rating';

@Component({
  selector: 'app-movie-detail',
  imports: [RouterLink, Poster, Rating, ErrorNotification],
  templateUrl: './movie-detail.html',
})
export class MovieDetail {
  private readonly router = inject(Router);
  private readonly moviesApi = inject(MoviesApi);
  private readonly diaryApi = inject(DiaryApi);
  private readonly watchlistApi = inject(WatchlistApi);

  /** Route parameter. */
  readonly id = input.required<string>();

  protected readonly yearOf = yearOf;
  protected readonly movie = signal<Movie | null>(null);
  protected readonly entries = signal<DiaryEntry[]>([]);
  protected readonly loading = signal(true);
  protected readonly notFound = signal(false);
  protected readonly busy = signal(false);
  protected readonly error = signal<ApiError | null>(null);

  constructor() {
    effect(() => this.load(this.id()));
  }

  protected toggleWatchlist(movie: Movie): void {
    const request = movie.onWatchlist
      ? this.watchlistApi.remove(movie.id)
      : this.watchlistApi.put(movie.id, null);
    this.run(request, () => this.movie.set({ ...movie, onWatchlist: !movie.onWatchlist }));
  }

  protected deleteMovie(movie: Movie): void {
    if (!confirm(`Delete "${movie.title}"? This cannot be undone.`)) {
      return;
    }
    this.run(this.moviesApi.delete(movie.id), () => this.router.navigate(['/movies']));
  }

  protected deleteEntry(entry: DiaryEntry): void {
    if (!confirm(`Delete the diary entry from ${entry.watchedOn}?`)) {
      return;
    }
    // Reload so that the movie's watch count and latest rating are up to date.
    this.run(this.diaryApi.delete(entry.id), () => this.load(this.id()));
  }

  private load(id: string): void {
    this.loading.set(true);
    forkJoin({ movie: this.moviesApi.get(id), entries: this.moviesApi.diaryEntries(id) }).subscribe(
      {
        next: ({ movie, entries }) => {
          this.movie.set(movie);
          this.entries.set(entries);
          this.notFound.set(false);
          this.loading.set(false);
        },
        error: (error) => {
          const apiError = toApiError(error);
          this.notFound.set(apiError.status === 404);
          this.error.set(apiError.status === 404 ? null : apiError);
          this.loading.set(false);
        },
      },
    );
  }

  private run(request: Observable<unknown>, onSuccess: () => void): void {
    this.busy.set(true);
    this.error.set(null);
    request.subscribe({
      next: () => {
        this.busy.set(false);
        onSuccess();
      },
      error: (error) => {
        this.busy.set(false);
        this.error.set(toApiError(error));
      },
    });
  }
}
