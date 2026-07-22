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
import { IconComponent } from '../../../core/icons/icon.component';

import { AuditsService } from '../../../core/services/audits.service';
import { Audit } from '../../../core/models';
import { LoadingComponent } from '../../../shared/components/loading/loading.component';
import { ErrorStateComponent } from '../../../shared/components/error-state/error-state.component';
import { PageHeaderComponent } from '../../../shared/components/page-header/page-header.component';
import { PageGuideComponent } from '../../../shared/components/page-guide/page-guide.component';
import { PageGuide } from '../../../core/models/page-guide.models';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';
import { AuditExecutionComponent } from './audit-execution.component';

type ViewState = 'loading' | 'ready' | 'error';

/** Contextual page guide for the fieldwork/execution screen (drives the walkthrough + the About panel). */
const AUDIT_EXECUTION_GUIDE: PageGuide = {
  id: 'audit-execution',
  titleKey: 'audits.exec.guideTitle',
  purposeKey: 'audits.exec.guide.purpose',
  descriptionKey: 'audits.exec.guide.description',
  actionKeys: [
    'audits.exec.guide.action.respond',
    'audits.exec.guide.action.evidence',
    'audits.exec.guide.action.exception',
    'audits.exec.guide.action.time',
  ],
  workflowKeys: ['audits.exec.guide.flow.assign', 'audits.exec.guide.flow.respond', 'audits.exec.guide.flow.evidence', 'audits.exec.guide.flow.review'],
  dependsOnKeys: ['audits.exec.guide.dep.template', 'audits.exec.guide.dep.team'],
  usedByKeys: ['audits.exec.guide.use.exceptions', 'audits.exec.guide.use.reports'],
  businessRuleKeys: ['audits.exec.guide.rule.autoReview', 'audits.exec.guide.rule.assignment'],
  tipKeys: ['audits.exec.guide.tip.evidence'],
  permissionKeys: ['audits.exec.guide.perm.view'],
};

/**
 * Dedicated fieldwork/execution screen for a single audit (`/audits/:id/execute`). Loads the audit, then hosts
 * {@link AuditExecutionComponent} (which resolves user names via the shared directory); reloads after mutations.
 */
@Component({
  selector: 'app-audit-execution-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    RouterLink,
    MatButtonModule,
    IconComponent,
    LoadingComponent,
    ErrorStateComponent,
    PageHeaderComponent,
    PageGuideComponent,
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
            <app-icon name="arrow_back" />
            <span>{{ 'audits.exec.backToAudit' | t }}</span>
          </a>
          <app-page-header [title]="a.name" [subtitle]="'audits.exec.pageSubtitle' | t">
            <span actions class="status-badge" [attr.data-status]="a.status">
              {{ a.status.replaceAll('_', ' ') }}
            </span>
          </app-page-header>
          <app-page-guide [guide]="guide" />
          <app-audit-execution
            [audit]="a"
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

  readonly state = signal<ViewState>('loading');
  readonly audit = signal<Audit | null>(null);

  readonly guide = AUDIT_EXECUTION_GUIDE;

  constructor() {
    queueMicrotask(() => this.fetch());
  }

  fetch(): void {
    this.state.set('loading');
    this.service.getById(this.id()).subscribe({
      next: (audit) => {
        this.audit.set(audit);
        this.state.set('ready');
      },
      error: () => this.state.set('error'),
    });
  }

  reload(): void {
    this.service.getById(this.id()).subscribe({ next: (a) => this.audit.set(a) });
  }
}
