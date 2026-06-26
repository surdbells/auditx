import {
  ChangeDetectionStrategy,
  Component,
  input,
  output,
} from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';

@Component({
  selector: 'app-error-state',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatButtonModule, MatIconModule],
  template: `
    <div class="error" role="alert">
      <mat-icon class="error__icon" aria-hidden="true">error_outline</mat-icon>
      <h3 class="error__title">{{ title() }}</h3>
      <p class="error__message">{{ message() }}</p>
      @if (showRetry()) {
        <button matButton="filled" type="button" (click)="retry.emit()">
          <mat-icon>refresh</mat-icon>
          {{ retryLabel() }}
        </button>
      }
    </div>
  `,
  styles: `
    .error {
      display: flex;
      flex-direction: column;
      align-items: center;
      text-align: center;
      gap: 0.5rem;
      padding: 3rem 1.5rem;
    }
    .error__icon {
      font-size: 48px;
      height: 48px;
      width: 48px;
      color: var(--mat-sys-error);
    }
    .error__title {
      margin: 0.5rem 0 0;
    }
    .error__message {
      margin: 0;
      max-width: 40ch;
      color: var(--mat-sys-on-surface-variant);
    }
    button {
      margin-top: 0.75rem;
    }
  `,
})
export class ErrorStateComponent {
  readonly title = input('Something went wrong');
  readonly message = input('We could not load this content. Please try again.');
  readonly showRetry = input(true);
  readonly retryLabel = input('Retry');

  readonly retry = output<void>();
}
