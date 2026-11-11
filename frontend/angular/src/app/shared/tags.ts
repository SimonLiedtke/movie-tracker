import { Component, input } from '@angular/core';
import { RouterLink } from '@angular/router';

/** The server accepts at most this many different tags per entry, each at most this long. */
export const MAX_TAGS = 20;
export const MAX_TAG_LENGTH = 50;

/**
 * Splits the text of a tags field ("horror, date night") into tags. Blank ones are left out; the server
 * stores tags trimmed, in lower case and without duplicates.
 */
export function parseTags(text: string): string[] {
  return text
    .split(',')
    .map((tag) => tag.trim())
    .filter((tag) => tag !== '');
}

/** The tags as the text of a tags field. */
export function formatTags(tags: readonly string[]): string {
  return tags.join(', ');
}

/** Why the text of a tags field is refused, or null if it is fine. Mirrors the server's rules. */
export function tagsError(text: string): string | null {
  const tags = parseTags(text);
  if (tags.some((tag) => tag.length > MAX_TAG_LENGTH)) {
    return `A tag can have at most ${MAX_TAG_LENGTH} characters.`;
  }
  const different = new Set(tags.map((tag) => tag.toLowerCase().replace(/\s+/g, ' ')));
  if (different.size > MAX_TAGS) {
    return `At most ${MAX_TAGS} different tags are allowed.`;
  }
  return null;
}

/** The tags of an entry. Each one links to the page of the route, listing everything with this tag. */
@Component({
  selector: 'app-tags',
  imports: [RouterLink],
  template: `@if (tags().length) {
    <div class="tags mb-0">
      @for (tag of tags(); track tag) {
        <a
          class="tag"
          [class.is-info]="tag === active()"
          [class.is-light]="tag !== active()"
          [routerLink]="route()"
          [queryParams]="{ tag: tag, page: null }"
          queryParamsHandling="merge"
          [attr.title]="'Show everything tagged ' + tag"
        >
          {{ tag }}
        </a>
      }
    </div>
  }`,
})
export class Tags {
  readonly tags = input.required<readonly string[]>();
  /** The route of the list that the tags filter, e.g. "/diary". */
  readonly route = input.required<string>();
  /** The tag the list is filtered by now, if any. */
  readonly active = input<string | null>();
}
