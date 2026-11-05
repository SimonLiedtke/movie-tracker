import { Component, inject, signal } from '@angular/core';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, ParamMap, Router, RouterLink } from '@angular/router';
import {
  Subject,
  catchError,
  debounceTime,
  distinctUntilChanged,
  map,
  of,
  switchMap,
  tap,
} from 'rxjs';
import { GenresApi, MoviesApi } from '../../core/api/api';
import { ApiError, toApiError } from '../../core/api/api-error';
import {
  MovieQuery,
  MovieRef,
  MovieSort,
  MovieSummary,
  PagedResponse,
} from '../../core/api/models';
import { yearOf } from '../../shared/dates';
import { ErrorNotification } from '../../shared/error-notification';
import { MoviePicker } from '../../shared/movie-picker';
import { Pagination } from '../../shared/pagination';
import { Poster } from '../../shared/poster';
import { Rating } from '../../shared/rating';

const PAGE_SIZE = 24;

const SORT_OPTIONS: { value: MovieSort; label: string }[] = [
  { value: 'title', label: 'Title (A–Z)' },
  { value: '-title', label: 'Title (Z–A)' },
  { value: '-releaseDate', label: 'Newest releases' },
  { value: 'releaseDate', label: 'Oldest releases' },
  { value: '-createdAt', label: 'Recently added' },
  { value: 'createdAt', label: 'First added' },
];

function parseQuery(params: ParamMap): MovieQuery {
  const number = (key: string) =>
    params.has(key) ? Number(params.get(key)) || undefined : undefined;
  const sort = params.get('sort') as MovieSort | null;
  return {
    search: params.get('search') ?? undefined,
    genreId: number('genreId'),
    year: number('year'),
    sort: SORT_OPTIONS.some((option) => option.value === sort) ? sort! : 'title',
    page: number('page') ?? 1,
    pageSize: PAGE_SIZE,
  };
}

@Component({
  selector: 'app-movie-list',
  imports: [RouterLink, Poster, Rating, Pagination, ErrorNotification, MoviePicker],
  templateUrl: './movie-list.html',
})
export class MovieList {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly moviesApi = inject(MoviesApi);

  protected readonly sortOptions = SORT_OPTIONS;
  protected readonly yearOf = yearOf;
  protected readonly genres = toSignal(
    inject(GenresApi)
      .list()
      .pipe(catchError(() => of([]))),
    {
      initialValue: [],
    },
  );
  protected readonly query = toSignal(this.route.queryParamMap.pipe(map(parseQuery)), {
    requireSync: true,
  });
  protected readonly result = signal<PagedResponse<MovieSummary> | null>(null);
  protected readonly loading = signal(true);
  protected readonly error = signal<ApiError | null>(null);

  private readonly searchInput = new Subject<string>();

  constructor() {
    this.route.queryParamMap
      .pipe(
        map(parseQuery),
        tap(() => this.loading.set(true)),
        switchMap((query) =>
          this.moviesApi.list(query).pipe(
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
        this.result.set(result);
        this.loading.set(false);
      });

    this.searchInput
      .pipe(debounceTime(300), distinctUntilChanged(), takeUntilDestroyed())
      // Not trimmed here: the input shows the URL value, so trimming would eat spaces while typing.
      .subscribe((search) => this.updateQuery({ search: search || undefined }, true));
  }

  protected openMovie(movie: MovieRef): void {
    this.router.navigate(['/movies', movie.id]);
  }

  protected onSearch(value: string): void {
    this.searchInput.next(value);
  }

  protected onFilter(key: 'genreId' | 'year' | 'sort', value: string): void {
    this.updateQuery({ [key]: value || undefined });
  }

  protected goToPage(page: number): void {
    this.router.navigate([], { queryParams: { page }, queryParamsHandling: 'merge' });
  }

  protected clearFilters(): void {
    this.router.navigate([]);
  }

  /** Changing a filter starts again at page 1. */
  private updateQuery(
    changes: Partial<Record<keyof MovieQuery, string | number | undefined>>,
    replaceUrl = false,
  ): void {
    this.router.navigate([], {
      queryParams: { ...changes, page: undefined },
      queryParamsHandling: 'merge',
      replaceUrl,
    });
  }
}
