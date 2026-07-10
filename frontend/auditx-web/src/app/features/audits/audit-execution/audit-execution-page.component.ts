import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  input,
  signal,
} from '@angular/core';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';

import { AuditsService } from '../../../core/services/audits.service';
import { UsersService } from '../../../core/services/users.service';
import { Audit, UserDto } from '../../../core/models';
import { LoadingComponent } from '../../../shared/components/loading/loading.component';
import { ErrorStateComponent } from '../../../shared/components/error-state/error-state.component';
import { PageHeaderComponent } from '../../../shared/components/page-header/page-header.component';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';
import { AuditExecutionComponent } from './audit-execution.component';

type ViewState = 'loading' | 'ready' | 'error';

/**
 * Dedicated fieldwork/execution screen for a single audit (`/audits/:id/execute`). Loads the audit and the
 * user directory itself, then hosts {@link AuditExecutionComponent}; reloads the audit after any mutation.
 */
@Component({
  selector: 'app-audit-execution-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    RouterLink,
    MatButtonModule,
    MatIconModule,
    LoadingComponent,
    ErrorStateComponent,
    PageHeaderComponent,
    AuditExecutionComponent,
    TranslatePipe,
  ],
  template: `
    @switch (state()) {
      @case ('loading') {
        <app-loading [message]="'audits.exec.loadingProgress' | t" />
      }
      @case ('error') {
        <app-error-state [message]="'audits.list.error' | t" (retry)="fetch()" />
      }
      @case ('ready') {
        @if (audit(); as a) {
          <a class="exec-page__back" [routerLink]="['/audits', a.id]">
            <mat-icon>arrow_back</mat-icon>
            <span>{{ 'audits.exec.backToAudit' | t }}</span>
          </a>
          <app-page-header [title]="a.name" [subtitle]="'audits.exec.pageSubtitle' | t">
            <span actions class="status-badge" [attr.data-status]="a.status">
              {{ a.status.replaceAll('_', ' ') }}
            </span>
          </app-page-header>
          <app-audit-execution
            [audit]="a"
            [userNames]="userNames()"
            (reloadRequested)="reload()"
          />
        }
      }
    }
  `,
  styles: `
    .exec-page__back {
      display: inline-flex;
      align-items: center;
      gap: 0.35rem;
      margin-bottom: 0.85rem;
      color: var(--mat-sys-on-surface-variant);
      text-decoration: none;
      font-size: 0.85rem;
      font-weight: 500;
    }
    .exec-page__back:hover {
      color: var(--mat-sys-primary);
    }
    .exec-page__back mat-icon {
      font-size: 18px;
      width: 18px;
      height: 18px;
    }
  `,
})
export class AuditExecutionPageComponent {
  /** Route param bound via withComponentInputBinding. */
  readonly id = input.required<string>();

  private readonly service = inject(AuditsService);
  private readonly users = inject(UsersService);

  readonly state = signal<ViewState>('loading');
  readonly audit = signal<Audit | null>(null);
  readonly userNames = signal<Record<string, string>>({});

  constructor() {
    queueMicrotask(() => this.fetch());
  }

  fetch(): void {
    this.state.set('loading');
    this.service.getById(this.id()).subscribe({
      next: (audit) => {
        this.audit.set(audit);
        this.state.set('ready');
        this.ensureUsers();
      },
      error: () => this.state.set('error'),
    });
  }

  reload(): void {
    this.service.getById(this.id()).subscribe({ next: (a) => this.audit.set(a) });
  }

  private ensureUsers(): void {
    if (Object.keys(this.userNames()).length) {
      return;
    }
    this.users.list({ status: 'active', pageSize: 0 }).subscribe({
      next: (page) => {
        const map: Record<string, string> = {};
        for (const u of page.items as UserDto[]) {
          map[u.id] = u.displayName;
        }
        this.userNames.set(map);
      },
      error: () => {
        // Non-fatal: ids display verbatim.
      },
    });
  }
}
