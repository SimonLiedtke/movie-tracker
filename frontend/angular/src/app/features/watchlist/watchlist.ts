import { Component, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { Subject, catchError, map, of, startWith, switchMap, tap } from 'rxjs';
import { WatchlistApi } from '../../core/api/api';
import { ApiError, toApiError } from '../../core/api/api-error';
import { PagedResponse, WatchlistItem } from '../../core/api/models';
import { yearOf } from '../../shared/dates';
import { ErrorNotification } from '../../shared/error-notification';
import { Pagination } from '../../shared/pagination';
import { Poster } from '../../shared/poster';

const PAGE_SIZE = 20;

@Component({
  selector: 'app-watchlist',
  imports: [RouterLink, Poster, Pagination, ErrorNotification],
  templateUrl: './watchlist.html',
})
export class Watchlist {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly watchlistApi = inject(WatchlistApi);

  protected readonly yearOf = yearOf;
  protected readonly result = signal<PagedResponse<WatchlistItem> | null>(null);
  protected readonly loading = signal(true);
  protected readonly error = signal<ApiError | null>(null);
  /** Movie ID of the item whose note is being edited. */
  protected readonly editing = signal<string | null>(null);
  protected readonly saving = signal(false);

  private readonly reload = new Subject<void>();

  constructor() {
    this.route.queryParamMap
      .pipe(
        map((params) => Number(params.get('page')) || 1),
        switchMap((page) =>
          this.reload.pipe(
            startWith(undefined),
            map(() => page),
          ),
        ),
        tap(() => this.loading.set(true)),
        switchMap((page) =>
          this.watchlistApi.list({ page, pageSize: PAGE_SIZE }).pipe(
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
    this.router.navigate([], { queryParams: { page } });
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
