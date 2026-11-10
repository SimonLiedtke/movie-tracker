import { Component, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, ParamMap, Params, Router, RouterLink } from '@angular/router';
import { Subject, catchError, map, of, startWith, switchMap, tap } from 'rxjs';
import { GenresApi, MoviesApi, WatchlistApi } from '../../core/api/api';
import { ApiError, toApiError } from '../../core/api/api-error';
import {
  Genre,
  GenreMatch,
  MovieRef,
  WatchlistItem,
  WatchlistPage,
  WatchlistQuery,
} from '../../core/api/models';
import { yearOf } from '../../shared/dates';
import { ErrorNotification } from '../../shared/error-notification';
import { MoviePicker } from '../../shared/movie-picker';
import { Pagination } from '../../shared/pagination';
import { Poster } from '../../shared/poster';

const PAGE_SIZE = 20;

const MATCH_OPTIONS: { value: GenreMatch; label: string }[] = [
  { value: 'any', label: 'Any of them' },
  { value: 'all', label: 'All of them' },
];

function parseQuery(params: ParamMap): WatchlistQuery {
  return {
    // Whatever is not a genre ID (e.g. from a hand-edited URL) is left out.
    genreIds: [...new Set(params.getAll('genreId').map(Number).filter(Number.isInteger))],
    genreMatch: params.get('genreMatch') === 'all' ? 'all' : 'any',
    page: Number(params.get('page')) || 1,
    pageSize: PAGE_SIZE,
  };
}

@Component({
  selector: 'app-watchlist',
  imports: [RouterLink, Poster, Pagination, ErrorNotification, MoviePicker],
  templateUrl: './watchlist.html',
})
export class Watchlist {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly watchlistApi = inject(WatchlistApi);
  private readonly moviesApi = inject(MoviesApi);

  protected readonly matchOptions = MATCH_OPTIONS;
  protected readonly yearOf = yearOf;
  protected readonly genres = toSignal(
    inject(GenresApi)
      .list()
      .pipe(catchError(() => of([]))),
    { initialValue: [] },
  );
  protected readonly query = toSignal(this.route.queryParamMap.pipe(map(parseQuery)), {
    requireSync: true,
  });
  protected readonly filtered = computed(() => (this.query().genreIds?.length ?? 0) > 0);
  protected readonly result = signal<WatchlistPage | null>(null);
  protected readonly loading = signal(true);
  protected readonly error = signal<ApiError | null>(null);
  /** Movie ID of the item whose note is being edited. */
  protected readonly editing = signal<string | null>(null);
  protected readonly saving = signal(false);

  private readonly reload = new Subject<void>();

  constructor() {
    this.route.queryParamMap
      .pipe(
        map(parseQuery),
        switchMap((query) =>
          this.reload.pipe(
            startWith(undefined),
            map(() => query),
          ),
        ),
        tap(() => this.loading.set(true)),
        switchMap((query) =>
          this.watchlistApi.list(query).pipe(
            tap(() => this.error.set(null)),
            catchError((error) => {
              this.error.set(toApiError(error));
              return of(null);
            }),
          ),
        ),
        takeUntilDestroyed(),
      )
      .subscribe((result) => {
        // E.g. after removing the only item on the last page.
        if (result && result.items.length === 0 && result.page > 1) {
          this.goToPage(Math.max(1, Math.ceil(result.totalCount / result.pageSize)));
          return;
        }
        this.result.set(result);
        this.loading.set(false);
      });
  }

  protected goToPage(page: number): void {
    this.router.navigate([], { queryParams: { page }, queryParamsHandling: 'merge' });
  }

  protected isSelected(genre: Genre): boolean {
    return this.query().genreIds?.includes(genre.id) ?? false;
  }

  /** Selects the genre, or deselects it if it is selected. */
  protected toggleGenre(genre: Genre): void {
    const selected = this.query().genreIds ?? [];
    const genreIds = this.isSelected(genre)
      ? selected.filter((id) => id !== genre.id)
      : [...selected, genre.id];
    this.updateFilter({ genreId: genreIds.length ? genreIds : undefined });
  }

  protected setGenreMatch(match: GenreMatch): void {
    // "any" is the default, so it needs no parameter.
    this.updateFilter({ genreMatch: match === 'all' ? 'all' : undefined });
  }

  protected clearFilter(): void {
    this.updateFilter({ genreId: undefined, genreMatch: undefined });
  }

  /** Changing the filter starts again at page 1. */
  private updateFilter(changes: Params): void {
    this.router.navigate([], {
      queryParams: { ...changes, page: undefined },
      queryParamsHandling: 'merge',
    });
  }

  /** Adds the movie at the top of the list (the newest come first). Already on it: nothing changes. */
  protected add(movie: MovieRef): void {
    // PUT replaces the note, so a movie that is already on the watchlist is left alone.
    this.moviesApi
      .get(movie.id)
      .pipe(
        switchMap((details) =>
          details.onWatchlist ? of(null) : this.watchlistApi.put(movie.id, null),
        ),
      )
      .subscribe({
        next: () => (this.result()?.page === 1 ? this.reload.next() : this.goToPage(1)),
        error: (error) => this.error.set(toApiError(error)),
      });
  }

  protected saveNote(item: WatchlistItem, note: string): void {
    this.saving.set(true);
    this.watchlistApi.put(item.movie.id, note.trim() || null).subscribe({
      next: (updated) => {
        this.saving.set(false);
        this.editing.set(null);
        this.result.update(
          (result) =>
            result && {
              ...result,
              items: result.items.map((i) => (i.movie.id === updated.movie.id ? updated : i)),
            },
        );
      },
      error: (error) => {
        this.saving.set(false);
        this.error.set(toApiError(error));
      },
    });
  }

  protected remove(item: WatchlistItem): void {
    this.watchlistApi.remove(item.movie.id).subscribe({
      next: () => this.reload.next(),
      error: (error) => this.error.set(toApiError(error)),
    });
  }
}
