import { Component, inject, signal } from '@angular/core';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, ParamMap, Router, RouterLink } from '@angular/router';
import { catchError, map, of, switchMap, tap } from 'rxjs';
import { ActivityLogApi } from '../../core/api/api';
import { ApiError, toApiError } from '../../core/api/api-error';
import { ActivityLogEntry, ActivityType, PageQuery, PagedResponse } from '../../core/api/models';
import { formatDateTime } from '../../shared/dates';
import { ErrorNotification } from '../../shared/error-notification';
import { Pagination } from '../../shared/pagination';

const PAGE_SIZE = 50;

/** What each kind of action is called in the log. */
const LABELS: Record<ActivityType, string> = {
  diaryEntryAdded: 'Logged a viewing',
  diaryEntryUpdated: 'Changed a viewing',
  diaryEntryDeleted: 'Deleted a viewing',
  watchlistItemAdded: 'Added to the watchlist',
  watchlistNoteChanged: 'Changed a watchlist note',
  watchlistItemRemoved: 'Removed from the watchlist',
  movieImported: 'Imported a movie from TMDB',
  movieAdded: 'Added a movie manually',
  movieUpdated: 'Changed a movie',
  movieDeleted: 'Deleted a movie',
  letterboxdImported: 'Imported from Letterboxd',
};

function parseQuery(params: ParamMap): PageQuery {
  return { page: Number(params.get('page')) || 1, pageSize: PAGE_SIZE };
}

@Component({
  selector: 'app-activity-log',
  imports: [RouterLink, Pagination, ErrorNotification],
  templateUrl: './activity-log.html',
})
export class ActivityLog {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly activityLogApi = inject(ActivityLogApi);

  protected readonly formatDateTime = formatDateTime;
  protected readonly label = (type: ActivityType) => LABELS[type] ?? type;
  protected readonly query = toSignal(this.route.queryParamMap.pipe(map(parseQuery)), {
    requireSync: true,
  });
  protected readonly result = signal<PagedResponse<ActivityLogEntry> | null>(null);
  protected readonly loading = signal(true);
  protected readonly error = signal<ApiError | null>(null);

  constructor() {
    this.route.queryParamMap
      .pipe(
        map(parseQuery),
        tap(() => this.loading.set(true)),
        switchMap((query) =>
          this.activityLogApi.list(query).pipe(
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
        // The page is past the end, e.g. from an old bookmark.
        if (result && result.items.length === 0 && result.page > 1) {
          this.goToPage(Math.max(1, Math.ceil(result.totalCount / result.pageSize)));
          return;
        }
        this.result.set(result);
        this.loading.set(false);
      });
  }

  protected goToPage(page: number): void {
    this.router.navigate([], { queryParams: { page: page > 1 ? page : undefined } });
  }
}
