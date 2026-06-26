import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';

@Component({
  selector: 'app-loading',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatProgressSpinnerModule],
  template: `
    <div class="loading" role="status" aria-live="polite">
      <mat-spinner [diameter]="diameter()" />
      <p class="loading__label">{{ message() }}</p>
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
  readonly message = input('Loading…');
  readonly diameter = input(48);
}
