import {
  ChangeDetectionStrategy,
  Component,
  input,
  output,
} from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';

import { TranslatePipe } from '../../../core/i18n/translate.pipe';

@Component({
  selector: 'app-error-state',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatButtonModule, MatIconModule, TranslatePipe],
  template: `
    <div class="error" role="alert">
      <mat-icon class="error__icon" aria-hidden="true">error_outline</mat-icon>
      <h3 class="error__title">{{ title() | t }}</h3>
      <p class="error__message">{{ message() | t }}</p>
      @if (showRetry()) {
        <button matButton="filled" type="button" (click)="retry.emit()">
          <mat-icon>refresh</mat-icon>
          {{ retryLabel() | t }}
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
      display: inline-flex;
      align-items: center;
      justify-content: center;
      width: 64px;
      height: 64px;
      border-radius: 18px;
      font-size: 32px;
      color: #dc2626;
      background: #fef2f2;
      border: 1px solid #fecaca;
      margin-bottom: 0.5rem;
    }
    .error__title {
      margin: 0.5rem 0 0;
      font-size: 1.05rem;
      font-weight: 600;
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
  readonly title = input('shared.error.title');
  readonly message = input('shared.error.message');
  readonly showRetry = input(true);
  readonly retryLabel = input('shared.error.retry');

  readonly retry = output<void>();
}
