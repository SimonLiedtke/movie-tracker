import { Component, input, signal } from '@angular/core';

@Component({
  selector: 'app-poster',
  template: `
    <figure class="image is-2by3">
      @if (url() && !failed()) {
        <img
          [src]="url()"
          [alt]="'Poster of ' + title()"
          loading="lazy"
          (error)="failed.set(true)"
        />
      } @else {
        <div class="has-ratio poster-placeholder" aria-hidden="true">🎬</div>
      }
    </figure>
  `,
})
export class Poster {
  readonly url = input<string | null>();
  readonly title = input('');

  protected readonly failed = signal(false);
}
