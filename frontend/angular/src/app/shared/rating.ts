import { Component, computed, input } from '@angular/core';

/** Formats a rating in half stars (1-10) as stars, e.g. 7 -> "★★★½". */
export function formatRating(halfStars: number | null | undefined): string {
  if (!halfStars) {
    return '';
  }
  return '★'.repeat(Math.floor(halfStars / 2)) + (halfStars % 2 === 1 ? '½' : '');
}

/** Choices for a rating select: 0.5 to 5 stars. */
export const RATING_OPTIONS: { value: number; label: string }[] = Array.from(
  { length: 10 },
  (_, i) => ({
    value: i + 1,
    label: `${formatRating(i + 1)} (${(i + 1) / 2})`,
  }),
);

@Component({
  selector: 'app-rating',
  template: `@if (halfStars()) {
    <span class="rating-stars" [attr.title]="title()" [attr.aria-label]="title()">{{
      stars()
    }}</span>
  }`,
})
export class Rating {
  readonly halfStars = input<number | null>();

  protected readonly stars = computed(() => formatRating(this.halfStars()));
  protected readonly title = computed(() => `${(this.halfStars() ?? 0) / 2} of 5 stars`);
}
