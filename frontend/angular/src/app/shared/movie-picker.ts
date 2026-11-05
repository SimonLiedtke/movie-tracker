import { Component, inject, input, model, output, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { Subject, catchError, debounceTime, distinctUntilChanged, map, of, switchMap } from 'rxjs';
import { MoviesApi } from '../core/api/api';
import { toApiError } from '../core/api/api-error';
import { MovieRef, MovieSearchResponse, MovieSearchResult } from '../core/api/models';
import { yearOf } from './dates';

/** Where the manual movie form continues after saving (see MovieForm). */
export type AfterManualAdd = 'diary' | 'watchlist';

/**
 * Search field to pick a movie: the user's manually added movies and TMDB's results.
 * Picking a TMDB movie imports it first, so the result is always a movie with an ID.
 *
 * - `[(selected)]`: shows the picked movie instead of the search field (for forms).
 * - `(picked)` with `[keepSelection]="false"`: emits and clears the field (for "add" actions).
 */
@Component({
  selector: 'app-movie-picker',
  imports: [RouterLink],
  template: `
    @if (keepSelection() && selected(); as movie) {
      <div class="field has-addons">
        <div class="control is-expanded">
          <input class="input is-static" readonly [value]="label(movie)" [attr.id]="inputId()" />
        </div>
        <div class="control">
          <button type="button" class="button" (click)="clear()">Change</button>
        </div>
      </div>
    } @else {
      <div class="dropdown is-block" [class.is-active]="open()">
        <div class="dropdown-trigger">
          <div class="control" [class.is-loading]="searching() || importing()">
            <input
              class="input"
              type="search"
              autocomplete="off"
              [placeholder]="placeholder()"
              [attr.id]="inputId()"
              [class.is-danger]="invalid()"
              [value]="term()"
              [disabled]="importing()"
              (input)="search($any($event.target).value)"
              (focus)="open.set(response() !== null)"
              (keydown.escape)="open.set(false)"
            />
          </div>
        </div>
        <div class="dropdown-menu" role="menu" style="width: 100%">
          <div class="dropdown-content" style="max-height: 24rem; overflow-y: auto">
            @if (response(); as response) {
              @if (!response.tmdbAvailable) {
                <div class="dropdown-item has-text-grey is-size-7">
                  TMDB search is not available right now, only your own movies are shown.
                </div>
              }
              @for (result of response.items; track result.movieId ?? 'tmdb-' + result.tmdbId) {
                <a
                  class="dropdown-item is-flex is-align-items-center"
                  href=""
                  (click)="$event.preventDefault(); pick(result)"
                >
                  <span class="is-flex-grow-1">{{ label(result) }}</span>
                  @if (result.tmdbId === null) {
                    <span class="tag is-warning is-light ml-2" title="Added manually by you"
                      >Yours</span
                    >
                  }
                </a>
              } @empty {
                <div class="dropdown-item has-text-grey">No matching movies.</div>
              }
              <hr class="dropdown-divider" />
              <a
                class="dropdown-item"
                routerLink="/movies/new"
                [queryParams]="{ title: term().trim() || null, next: afterManualAdd() ?? null }"
              >
                Can't find it? <strong>Add it manually</strong>
              </a>
            }
          </div>
        </div>
      </div>
      @if (importError(); as message) {
        <p class="help is-danger">{{ message }}</p>
      }
    }
  `,
})
export class MoviePicker {
  readonly selected = model<MovieRef | null>(null);
  readonly picked = output<MovieRef>();
  readonly keepSelection = input(true);
  readonly afterManualAdd = input<AfterManualAdd>();
  readonly inputId = input<string>();
  readonly invalid = input(false);
  readonly placeholder = input('Search a movie by title');

  protected readonly term = signal('');
  protected readonly response = signal<MovieSearchResponse | null>(null);
  protected readonly searching = signal(false);
  protected readonly importing = signal(false);
  protected readonly importError = signal<string | null>(null);
  protected readonly open = signal(false);

  private readonly moviesApi = inject(MoviesApi);
  private readonly terms = new Subject<string>();

  constructor() {
    this.terms
      .pipe(
        map((term) => term.trim()),
        debounceTime(250),
        distinctUntilChanged(),
        switchMap((term) => {
          if (!term) {
            return of(null);
          }
          this.searching.set(true);
          return this.moviesApi
            .search(term)
            .pipe(catchError(() => of<MovieSearchResponse>({ items: [], tmdbAvailable: false })));
        }),
        takeUntilDestroyed(),
      )
      .subscribe((response) => {
        this.searching.set(false);
        this.response.set(response);
        this.open.set(response !== null);
      });
  }

  protected search(term: string): void {
    this.term.set(term);
    this.importError.set(null);
    this.terms.next(term);
  }

  protected pick(result: MovieSearchResult): void {
    this.open.set(false);
    if (result.movieId) {
      this.done({
        id: result.movieId,
        title: result.title,
        releaseDate: result.releaseDate,
        posterUrl: result.posterUrl,
      });
      return;
    }

    this.importing.set(true);
    this.importError.set(null);
    this.moviesApi.importFromTmdb(result.tmdbId!).subscribe({
      next: (movie) => {
        this.importing.set(false);
        this.done(movie);
      },
      error: (error) => {
        this.importing.set(false);
        this.importError.set(toApiError(error).message);
      },
    });
  }

  protected clear(): void {
    this.selected.set(null);
    this.reset();
  }

  protected label(movie: { title: string; releaseDate: string | null }): string {
    const year = yearOf(movie.releaseDate);
    return year ? `${movie.title} (${year})` : movie.title;
  }

  private done(movie: MovieRef): void {
    const ref: MovieRef = {
      id: movie.id,
      title: movie.title,
      releaseDate: movie.releaseDate,
      posterUrl: movie.posterUrl,
    };
    this.picked.emit(ref);
    if (this.keepSelection()) {
      this.selected.set(ref);
    }
    this.reset();
  }

  private reset(): void {
    this.term.set('');
    this.response.set(null);
    this.terms.next('');
  }
}
