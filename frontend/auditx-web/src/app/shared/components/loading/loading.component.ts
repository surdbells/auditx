import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';

import { TranslatePipe } from '../../../core/i18n/translate.pipe';

@Component({
  selector: 'app-loading',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatProgressSpinnerModule, TranslatePipe],
  template: `
    <div class="loading" role="status" aria-live="polite">
      <mat-spinner [diameter]="diameter()" />
      <p class="loading__label">{{ message() | t }}</p>
    </div>
  `,
  styles: `
    .loading {
      display: flex;
      flex-direction: column;
      align-items: center;
      justify-content: center;
      gap: 1rem;
      padding: 2.5rem 1rem;
      min-height: 160px;
    }
    .loading__label {
      margin: 0;
      color: var(--mat-sys-on-surface-variant);
    }
  `,
})
export class LoadingComponent {
  readonly message = input('shared.loading.message');
  readonly diameter = input(48);
}
