import { Component, computed, input, output } from '@angular/core';

@Component({
  selector: 'app-pagination',
  template: `
    @if (pageCount() > 1) {
      <nav class="pagination is-centered mt-5" role="navigation" aria-label="pagination">
        <button
          class="pagination-previous"
          [disabled]="page() <= 1"
          (click)="pageChange.emit(page() - 1)"
        >
          Previous
        </button>
        <button
          class="pagination-next"
          [disabled]="page() >= pageCount()"
          (click)="pageChange.emit(page() + 1)"
        >
          Next
        </button>
        <ul class="pagination-list">
          @for (p of pages(); track $index) {
            <li>
              @if (p === null) {
                <span class="pagination-ellipsis">&hellip;</span>
              } @else {
                <button
                  class="pagination-link"
                  [class.is-current]="p === page()"
                  [attr.aria-current]="p === page() ? 'page' : null"
                  (click)="pageChange.emit(p)"
                >
                  {{ p }}
                </button>
              }
            </li>
          }
        </ul>
      </nav>
    }
  `,
})
export class Pagination {
  readonly page = input.required<number>();
  readonly pageSize = input.required<number>();
  readonly totalCount = input.required<number>();
  readonly pageChange = output<number>();

  protected readonly pageCount = computed(() => Math.ceil(this.totalCount() / this.pageSize()));

  /** First, last, and the pages around the current one; null marks a gap. */
  protected readonly pages = computed(() => {
    const current = this.page();
    const last = this.pageCount();
    const result: (number | null)[] = [];
    for (let p = 1; p <= last; p++) {
      if (p === 1 || p === last || Math.abs(p - current) <= 1) {
        result.push(p);
      } else if (result.at(-1) !== null) {
        result.push(null);
      }
    }
    return result;
  });
}
