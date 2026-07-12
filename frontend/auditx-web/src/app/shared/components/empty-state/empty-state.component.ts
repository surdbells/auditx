import {
  ChangeDetectionStrategy,
  Component,
  input,
  output,
} from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { IconComponent } from '../../../core/icons/icon.component';

import { TranslatePipe } from '../../../core/i18n/translate.pipe';

@Component({
  selector: 'app-empty-state',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatButtonModule, IconComponent, TranslatePipe],
  template: `
    <div class="empty">
      <app-icon class="empty__icon" aria-hidden="true" [name]="icon()" />
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
      display: inline-flex;
      align-items: center;
      justify-content: center;
      width: 64px;
      height: 64px;
      border-radius: 18px;
      font-size: 32px;
      color: #4f46e5;
      background: #eef2ff;
      border: 1px solid #e0e7ff;
      margin-bottom: 0.5rem;
    }
    .empty__title {
      margin: 0.5rem 0 0;
      font-size: 1.05rem;
      font-weight: 600;
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
