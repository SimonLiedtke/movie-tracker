import { Component, input, output } from '@angular/core';
import { ApiError } from '../core/api/api-error';

@Component({
  selector: 'app-error-notification',
  template: `
    @if (error(); as error) {
      <div class="notification is-danger is-light" role="alert">
        <button class="delete" aria-label="Dismiss" (click)="dismiss.emit()"></button>
        <p>{{ error.message }}</p>
        @if (error.details.length) {
          <ul class="mt-2">
            @for (detail of error.details; track $index) {
              <li>&bull; {{ detail }}</li>
            }
          </ul>
        }
      </div>
    }
  `,
})
export class ErrorNotification {
  readonly error = input<ApiError | null>(null);
  readonly dismiss = output<void>();
}
