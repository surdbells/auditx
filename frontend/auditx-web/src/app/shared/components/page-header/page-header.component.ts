import { ChangeDetectionStrategy, Component, input } from '@angular/core';

@Component({
  selector: 'app-page-header',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <header class="page-header">
      <div class="page-header__text">
        <h1 class="page-header__title">{{ title() }}</h1>
        @if (subtitle()) {
          <p class="page-header__subtitle">{{ subtitle() }}</p>
        }
      </div>
      <div class="page-header__actions">
        <ng-content select="[actions]" />
      </div>
    </header>
  `,
  styles: `
    .page-header {
      display: flex;
      flex-wrap: wrap;
      gap: 1rem;
      align-items: flex-start;
      justify-content: space-between;
      margin-bottom: 1.75rem;
    }
    .page-header__title {
      margin: 0;
      font: var(--mat-sys-headline-small);
      font-weight: 700;
      letter-spacing: -0.015em;
    }
    .page-header__subtitle {
      margin: 0.35rem 0 0;
      max-width: 68ch;
      color: var(--mat-sys-on-surface-variant);
    }
    .page-header__actions {
      display: flex;
      gap: 0.5rem;
      flex-wrap: wrap;
    }
    @media (max-width: 599px) {
      .page-header {
        margin-bottom: 1.25rem;
      }
      .page-header__actions {
        width: 100%;
      }
    }
  `,
})
export class PageHeaderComponent {
  readonly title = input.required<string>();
  readonly subtitle = input('');
}
