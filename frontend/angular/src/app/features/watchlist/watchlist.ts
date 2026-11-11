import { Component, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, ParamMap, Params, Router, RouterLink } from '@angular/router';
import {
  Subject,
  catchError,
  debounceTime,
  distinctUntilChanged,
  map,
  of,
  startWith,
  switchMap,
  tap,
} from 'rxjs';
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
import { formatDate, yearOf } from '../../shared/dates';
import { ErrorNotification } from '../../shared/error-notification';
import { MoviePicker } from '../../shared/movie-picker';
import { Pagination } from '../../shared/pagination';
import { Poster } from '../../shared/poster';
import { Tags } from '../../shared/tags';

const PAGE_SIZE = 20;
const SEARCH_DEBOUNCE_MS = 300;

const MATCH_OPTIONS: { value: GenreMatch; label: string }[] = [
  { value: 'any', label: 'Any of them' },
  { value: 'all', label: 'All of them' },
];

/**
 * Quick picks for the runtime, so that the usual questions need one click. They do not overlap:
 * "Under 90 min" is up to 89 minutes, "Over 120 min" from 121.
 */
const RUNTIME_PRESETS: { label: string; from?: number; to?: number }[] = [
  { label: 'Under 90 min', to: 89 },
  { label: '90 to 120 min', from: 90, to: 120 },
  { label: 'Over 120 min', from: 121 },
];

type RuntimePreset = (typeof RUNTIME_PRESETS)[number];

/** A whole number of minutes; anything else (e.g. from a hand-edited URL) is left out. */
function parseMinutes(value: string | null): number | undefined {
  const number = Number(value);
  return Number.isInteger(number) && number > 0 ? number : undefined;
}

/** E.g. "runtime of at least 90 minutes"; undefined without runtime. */
function describeRuntime(from?: number, to?: number): string | undefined {
  if (from && to) {
    return `runtime of ${from} to ${to} minutes`;
  }
  if (from) {
    return `runtime of at least ${from} minutes`;
  }
  if (to) {
    return `runtime of at most ${to} minutes`;
  }
  return undefined;
}

/** E.g. "released from May 25, 1979 to November 3, 1989" or "added until ..."; undefined without dates. */
function describePeriod(what: string, from?: string, to?: string): string | undefined {
  if (from && to) {
    return `${what} from ${formatDate(from)} to ${formatDate(to)}`;
  }
  if (from) {
    return `${what} from ${formatDate(from)}`;
  }
  if (to) {
    return `${what} until ${formatDate(to)}`;
  }
  return undefined;
}

/**
 * What the request depends on. The match mode means nothing without genres, and spaces around the
 * search text do not matter, so changing them must not load the same list again.
 */
function requestKey(query: WatchlistQuery): string {
  return JSON.stringify({
    ...query,
    genreMatch: query.genreIds?.length ? query.genreMatch : null,
    search: query.search?.trim(),
  });
}

function parseQuery(params: ParamMap): WatchlistQuery {
  return {
    // Whatever is not a genre ID (e.g. from a hand-edited URL) is left out.
    genreIds: [...new Set(params.getAll('genreId').map(Number).filter(Number.isInteger))],
    genreMatch: params.get('genreMatch') === 'all' ? 'all' : 'any',
    releasedFrom: params.get('releasedFrom') || undefined,
    releasedTo: params.get('releasedTo') || undefined,
    addedFrom: params.get('addedFrom') || undefined,
    addedTo: params.get('addedTo') || undefined,
    runtimeFrom: parseMinutes(params.get('runtimeFrom')),
    runtimeTo: parseMinutes(params.get('runtimeTo')),
    // Not trimmed: the field shows the URL's value, and trimming would eat spaces while typing.
    search: params.get('search')?.trim() ? params.get('search')! : undefined,
    tag: params.get('tag')?.trim() || undefined,
    page: Number(params.get('page')) || 1,
    pageSize: PAGE_SIZE,
  };
}

@Component({
  selector: 'app-watchlist',
  imports: [RouterLink, Poster, Pagination, ErrorNotification, MoviePicker, Tags],
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
  private readonly hasGenres = computed(() => (this.query().genreIds?.length ?? 0) > 0);
  protected readonly filtered = computed(() => {
    const { releasedFrom, releasedTo, addedFrom, addedTo, runtimeFrom, runtimeTo, search, tag } =
      this.query();
    return (
      this.hasGenres() ||
      !!(
        releasedFrom ||
        releasedTo ||
        addedFrom ||
        addedTo ||
        runtimeFrom ||
        runtimeTo ||
        search ||
        tag
      )
    );
  });
  /** What the filter selects, e.g. "with any of the selected genres, released from May 25, 1979". */
  protected readonly filterDescription = computed(() => {
    const {
      genreMatch,
      releasedFrom,
      releasedTo,
      addedFrom,
      addedTo,
      runtimeFrom,
      runtimeTo,
      search,
      tag,
    } = this.query();
    const genreText = this.hasGenres()
      ? `with ${genreMatch === 'all' ? 'all' : 'any'} of the selected genres`
      : undefined;
    return [
      search ? `matching "${search.trim()}"` : undefined,
      tag ? `tagged "${tag}"` : undefined,
      genreText,
      describeRuntime(runtimeFrom, runtimeTo),
      describePeriod('released', releasedFrom, releasedTo),
      describePeriod('added', addedFrom, addedTo),
    ]
      .filter((part) => part !== undefined)
      .join(', ');
  });
  protected readonly runtimePresets = RUNTIME_PRESETS;
  protected readonly result = signal<WatchlistPage | null>(null);
  protected readonly loading = signal(true);
  protected readonly error = signal<ApiError | null>(null);
  /** Movie ID of the item whose note is being edited. */
  protected readonly editing = signal<string | null>(null);
  protected readonly saving = signal(false);

  private readonly reload = new Subject<void>();
  private readonly searchInput = new Subject<string>();

  constructor() {
    this.searchInput
      .pipe(debounceTime(SEARCH_DEBOUNCE_MS), takeUntilDestroyed())
      .subscribe((value) => {
        if (value !== (this.query().search ?? '')) {
          this.updateFilter({ search: value.trim() ? value : undefined }, true);
        }
      });

    this.route.queryParamMap
      .pipe(
        map(parseQuery),
        distinctUntilChanged((a, b) => requestKey(a) === requestKey(b)),
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

  protected onSearch(value: string): void {
    this.searchInput.next(value);
  }

  protected onTag(value: string): void {
    this.updateFilter({ tag: value.trim() || undefined });
  }

  protected onRuntime(key: 'runtimeFrom' | 'runtimeTo', value: string): void {
    this.updateFilter({ [key]: value || undefined });
  }

  protected isRuntimePreset(preset: RuntimePreset): boolean {
    return this.query().runtimeFrom === preset.from && this.query().runtimeTo === preset.to;
  }

  /** Sets the runtime to the quick pick, or removes it if it is selected already. */
  protected toggleRuntimePreset(preset: RuntimePreset): void {
    const selected = this.isRuntimePreset(preset);
    this.updateFilter({
      runtimeFrom: selected ? undefined : preset.from,
      runtimeTo: selected ? undefined : preset.to,
    });
  }

  protected onDate(
    key: 'releasedFrom' | 'releasedTo' | 'addedFrom' | 'addedTo',
    value: string,
  ): void {
    this.updateFilter({ [key]: value || undefined });
  }

  protected clearFilter(): void {
    this.updateFilter({
      genreId: undefined,
      genreMatch: undefined,
      releasedFrom: undefined,
      releasedTo: undefined,
      addedFrom: undefined,
      addedTo: undefined,
      runtimeFrom: undefined,
      runtimeTo: undefined,
      search: undefined,
      tag: undefined,
    });
  }

  /** Changing the filter starts again at page 1. */
  private updateFilter(changes: Params, replaceUrl = false): void {
    this.router.navigate([], {
      queryParams: { ...changes, page: undefined },
      queryParamsHandling: 'merge',
      replaceUrl,
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
    this.watchlistApi.put(item.movie.id, note.trim() || null, item.tags).subscribe({
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
