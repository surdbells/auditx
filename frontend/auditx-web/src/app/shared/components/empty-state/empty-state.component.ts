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
  selector: 'app-empty-state',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatButtonModule, MatIconModule, TranslatePipe],
  template: `
    <div class="empty">
      <mat-icon class="empty__icon" aria-hidden="true">{{ icon() }}</mat-icon>
      <h3 class="empty__title">{{ title() | t }}</h3>
      @if (message()) {
        <p class="empty__message">{{ message() | t }}</p>
      }
      @if (actionLabel()) {
        <button matButton="filled" type="button" (click)="action.emit()">
          {{ actionLabel() | t }}
        </button>
      }
    </div>
  `,
  styles: `
    .empty {
      display: flex;
      flex-direction: column;
      align-items: center;
      text-align: center;
      gap: 0.5rem;
      padding: 3rem 1.5rem;
      color: var(--mat-sys-on-surface-variant);
    }
    .empty__icon {
      font-size: 48px;
      height: 48px;
      width: 48px;
      opacity: 0.6;
    }
    .empty__title {
      margin: 0.5rem 0 0;
      color: var(--mat-sys-on-surface);
    }
    .empty__message {
      margin: 0;
      max-width: 36ch;
    }
    button {
      margin-top: 0.75rem;
    }
  `,
})
export class EmptyStateComponent {
  readonly icon = input('inbox');
  readonly title = input('shared.empty.title');
  readonly message = input('');
  readonly actionLabel = input('');

  readonly action = output<void>();
}
