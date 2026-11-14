import { Component, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, ParamMap, Router, RouterLink } from '@angular/router';
import { Subject, catchError, map, of, startWith, switchMap, tap } from 'rxjs';
import { DiaryApi } from '../../core/api/api';
import { ApiError, toApiError } from '../../core/api/api-error';
import { DiaryEntry, DiaryQuery, PagedResponse } from '../../core/api/models';
import { formatDate, formatDay, formatMonth, yearOf } from '../../shared/dates';
import { ErrorNotification } from '../../shared/error-notification';
import { Pagination } from '../../shared/pagination';
import { RATING_OPTIONS, Rating } from '../../shared/rating';
import { Tags } from '../../shared/tags';

const PAGE_SIZE = 25;

function parseQuery(params: ParamMap): DiaryQuery {
  return {
    from: params.get('from') ?? undefined,
    to: params.get('to') ?? undefined,
    tag: params.get('tag')?.trim() || undefined,
    ratingFrom: Number(params.get('ratingFrom')) || undefined,
    ratingTo: Number(params.get('ratingTo')) || undefined,
    page: Number(params.get('page')) || 1,
    pageSize: PAGE_SIZE,
  };
}

interface MonthGroup {
  /** "YYYY-MM" */
  key: string;
  label: string;
  entries: DiaryEntry[];
}

@Component({
  selector: 'app-diary-list',
  imports: [RouterLink, Rating, Pagination, ErrorNotification, Tags],
  templateUrl: './diary-list.html',
})
export class DiaryList {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly diaryApi = inject(DiaryApi);

  protected readonly ratingOptions = RATING_OPTIONS;
  protected readonly yearOf = yearOf;
  protected readonly formatDay = formatDay;
  protected readonly query = toSignal(this.route.queryParamMap.pipe(map(parseQuery)), {
    requireSync: true,
  });
  protected readonly result = signal<PagedResponse<DiaryEntry> | null>(null);
  /** The current page's entries grouped by month, in the order the API returned them. */
  protected readonly groups = computed<MonthGroup[]>(() => {
    const groups: MonthGroup[] = [];
    for (const entry of this.result()?.items ?? []) {
      const key = entry.watchedOn.slice(0, 7);
      let group = groups.at(-1);
      if (group?.key !== key) {
        group = { key, label: formatMonth(entry.watchedOn), entries: [] };
        groups.push(group);
      }
      group.entries.push(entry);
    }
    return groups;
  });
  protected readonly loading = signal(true);
  protected readonly error = signal<ApiError | null>(null);

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
          this.diaryApi.list(query).pipe(
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
        // E.g. after deleting the only entry on the last page.
        if (result && result.items.length === 0 && result.page > 1) {
          this.goToPage(Math.max(1, Math.ceil(result.totalCount / result.pageSize)));
          return;
        }
        this.result.set(result);
        this.loading.set(false);
      });
  }

  protected onFilter(key: 'from' | 'to' | 'tag' | 'ratingFrom' | 'ratingTo', value: string): void {
    this.router.navigate([], {
      queryParams: { [key]: value || undefined, page: undefined },
      queryParamsHandling: 'merge',
    });
  }

  /** Whether a filter other than the page is set. */
  protected readonly hasFilter = computed(() => {
    const { from, to, tag, ratingFrom, ratingTo } = this.query();
    return !!(from || to || tag || ratingFrom || ratingTo);
  });

  protected clearFilters(): void {
    this.router.navigate([]);
  }

  protected goToPage(page: number): void {
    this.router.navigate([], { queryParams: { page }, queryParamsHandling: 'merge' });
  }

  protected deleteEntry(entry: DiaryEntry): void {
    if (
      !confirm(
        `Delete the diary entry for "${entry.movie.title}" from ${formatDate(entry.watchedOn)}?`,
      )
    ) {
      return;
    }
    this.diaryApi.delete(entry.id).subscribe({
      next: () => this.reload.next(),
      error: (error) => this.error.set(toApiError(error)),
    });
  }
}
