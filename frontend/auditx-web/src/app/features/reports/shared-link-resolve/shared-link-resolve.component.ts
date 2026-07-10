import {
  ChangeDetectionStrategy,
  Component,
  inject,
  input,
  signal,
} from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { Router, RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';

import { SharedLinksService } from '../../../core/services/shared-links.service';
import { LoadingComponent } from '../../../shared/components/loading/loading.component';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';

type ResolveState = 'loading' | 'invalid' | 'forbidden' | 'error';

/**
 * Resolves a shared slug (/s/:slug) and forwards to the target (D3-B). The link is only a reference: this navigates
 * to the report route, which still enforces the viewer's own permission. A missing/revoked/expired slug (404) or a
 * viewer without access (403) lands on a clear message rather than the report.
 */
@Component({
  selector: 'app-shared-link-resolve',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, MatButtonModule, MatIconModule, LoadingComponent, TranslatePipe],
  template: `
    @switch (state()) {
      @case ('loading') {
        <app-loading [message]="'sharedLink.resolving' | t" />
      }
      @default {
        <div class="slr">
          <mat-icon class="slr__icon">{{ state() === 'forbidden' ? 'lock' : 'link_off' }}</mat-icon>
          <h2 class="slr__title">{{ 'sharedLink.' + state() + '.title' | t }}</h2>
          <p class="slr__message">{{ 'sharedLink.' + state() + '.message' | t }}</p>
          <a matButton="filled" routerLink="/reports">{{ 'sharedLink.goToReports' | t }}</a>
        </div>
      }
    }
  `,
  styles: `
    .slr {
      max-width: 32rem;
      margin: 4rem auto;
      text-align: center;
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: 0.5rem;
    }
    .slr__icon {
      font-size: 3rem;
      width: 3rem;
      height: 3rem;
      color: var(--mat-sys-on-surface-variant);
    }
    .slr__title {
      font: var(--mat-sys-headline-small);
      margin: 0.5rem 0 0;
    }
    .slr__message {
      color: var(--mat-sys-on-surface-variant);
      margin: 0 0 1rem;
    }
  `,
})
export class SharedLinkResolveComponent {
  /** Route param bound via withComponentInputBinding. */
  readonly slug = input.required<string>();

  private readonly service = inject(SharedLinksService);
  private readonly router = inject(Router);

  readonly state = signal<ResolveState>('loading');

  constructor() {
    queueMicrotask(() => this.resolve());
  }

  private resolve(): void {
    this.service.resolve(this.slug()).subscribe({
      next: (target) => {
        if (target.targetType === 'report') {
          void this.router.navigate(['/reports', target.targetId], { replaceUrl: true });
        } else {
          this.state.set('invalid');
        }
      },
      error: (err: HttpErrorResponse) => {
        this.state.set(err.status === 403 ? 'forbidden' : err.status === 404 ? 'invalid' : 'error');
      },
    });
  }
}
