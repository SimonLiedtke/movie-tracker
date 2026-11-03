import { Component, inject, input, model, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Subject, catchError, debounceTime, distinctUntilChanged, map, of, switchMap } from 'rxjs';
import { MoviesApi } from '../core/api/api';
import { MovieRef } from '../core/api/models';
import { yearOf } from './dates';

/** Search field that lets the user pick one movie. */
@Component({
  selector: 'app-movie-picker',
  template: `
    @if (selected(); as movie) {
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
          <div class="control" [class.is-loading]="searching()">
            <input
              class="input"
              type="search"
              autocomplete="off"
              placeholder="Search a movie by title"
              [attr.id]="inputId()"
              [class.is-danger]="invalid()"
              (input)="search($any($event.target).value)"
              (focus)="open.set(results().length > 0)"
              (keydown.escape)="open.set(false)"
            />
          </div>
        </div>
        <div class="dropdown-menu is-fullwidth" role="menu" style="width: 100%">
          <div class="dropdown-content">
            @for (movie of results(); track movie.id) {
              <a class="dropdown-item" href="" (click)="$event.preventDefault(); pick(movie)">{{
                label(movie)
              }}</a>
            } @empty {
              <div class="dropdown-item has-text-grey">No matching movies.</div>
            }
          </div>
        </div>
      </div>
    }
  `,
})
export class MoviePicker {
  readonly selected = model<MovieRef | null>(null);
  readonly inputId = input<string>();
  readonly invalid = input(false);

  protected readonly results = signal<MovieRef[]>([]);
  protected readonly searching = signal(false);
  protected readonly open = signal(false);

  private readonly terms = new Subject<string>();

  constructor() {
    const moviesApi = inject(MoviesApi);
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
          return moviesApi.list({ search: term, pageSize: 10 }).pipe(
            map((page) => page.items),
            catchError(() => of([])),
          );
        }),
        takeUntilDestroyed(),
      )
      .subscribe((movies) => {
        this.searching.set(false);
        this.results.set(movies ?? []);
        this.open.set(movies !== null);
      });
  }

  protected search(term: string): void {
    this.terms.next(term);
  }

  protected pick(movie: MovieRef): void {
    this.selected.set(movie);
    this.open.set(false);
  }

  protected clear(): void {
    this.selected.set(null);
    this.results.set([]);
    this.terms.next('');
  }

  protected label(movie: MovieRef): string {
    const year = yearOf(movie.releaseDate);
    return year ? `${movie.title} (${year})` : movie.title;
  }
}
