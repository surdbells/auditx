import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule } from '@angular/material/dialog';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { RouterLink } from '@angular/router';

import { TranslatePipe } from '../../../core/i18n/translate.pipe';
import { TranslationService } from '../../../core/i18n/translation.service';
import { IconComponent } from '../../../core/icons/icon.component';
import { EntityAuditHistory, EntityAuditHistoryItem } from '../../../core/models';
import { Permissions } from '../../../core/permissions';
import { AuthService } from '../../../core/services/auth.service';
import { ReferenceDataLookupService } from '../../../core/services/reference-data-lookup.service';
import { UniverseService } from '../../../core/services/universe.service';

export interface EntityAuditHistoryDialogData {
  entityId: string;
  /** Shown while the history loads. */
  entityName: string;
}

/** i18n key for an audit status as the API sends it (snake_case). */
const STATUS_KEYS: Record<string, string> = {
  draft: 'audits.status.draft',
  planned: 'audits.status.planned',
  in_progress: 'audits.status.inProgress',
  under_review: 'audits.status.underReview',
  completed: 'audits.status.completed',
  cancelled: 'audits.status.cancelled',
};

/**
 * The full audit history of one audit-universe entity: every audit linked to it — launched from the annual plan,
 * created ad hoc, or a self-assessment — with what each one found. Opened from the "Last audited" cell, so the date
 * in the table is always one click away from the audits behind it.
 */
@Component({
  selector: 'app-entity-audit-history-dialog',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DatePipe, RouterLink, MatDialogModule, MatButtonModule, MatProgressBarModule, IconComponent, TranslatePipe],
  template: `
    <h2 mat-dialog-title class="hist__title">
      <span>{{ 'universe.history.title' | t }}</span>
      <span class="hist__entity">{{ history()?.entityName ?? data.entityName }}</span>
    </h2>

    <mat-dialog-content class="hist">
      @if (loading()) {
        <mat-progress-bar mode="indeterminate" />
      } @else if (failed()) {
        <p class="hist__empty" role="alert">{{ 'universe.history.loadFailed' | t }}</p>
      } @else if (history(); as h) {
        <!-- Headline figures: what a reviewer asks first. -->
        <dl class="hist__stats">
          <div class="hist__stat">
            <dt>{{ 'universe.field.lastAudited' | t }}</dt>
            <dd>{{ h.lastAuditedAt ? (h.lastAuditedAt | date: 'mediumDate') : ('universe.value.never' | t) }}</dd>
          </div>
          <div class="hist__stat">
            <dt>{{ 'universe.history.stat.completed' | t }}</dt>
            <dd>{{ h.completedAudits }}<span class="hist__of"> / {{ h.totalAudits }}</span></dd>
          </div>
          <div class="hist__stat">
            <dt>{{ 'universe.history.stat.inFlight' | t }}</dt>
            <dd>{{ h.inFlightAudits }}</dd>
          </div>
          <div class="hist__stat">
            <dt>{{ 'universe.history.stat.openFindings' | t }}</dt>
            <dd [class.hist__warn]="h.openFindings > 0">
              {{ h.openFindings }}<span class="hist__of"> / {{ h.totalFindings }}</span>
            </dd>
          </div>
          @if (h.selfAssessments > 0) {
            <div class="hist__stat">
              <dt>{{ 'universe.history.stat.selfAssessments' | t }}</dt>
              <dd>{{ h.selfAssessments }}</dd>
            </div>
          }
          @if (h.expectedAuditsPerYear) {
            <div class="hist__stat">
              <dt>{{ 'universe.history.stat.expectedPerYear' | t }}</dt>
              <dd>{{ h.expectedAuditsPerYear }}</dd>
            </div>
          }
        </dl>

        @if (h.isArchived) {
          <p class="hist__note">
            <app-icon name="archive" aria-hidden="true" />
            <span>{{ 'universe.history.archivedNote' | t }}</span>
          </p>
        }

        @if (h.audits.length === 0) {
          <div class="hist__empty">
            <app-icon name="history" aria-hidden="true" />
            <p>{{ 'universe.history.empty' | t }}</p>
            <p class="hist__empty-hint">{{ 'universe.history.emptyHint' | t }}</p>
          </div>
        } @else {
          <ol class="hist__list">
            @for (a of h.audits; track a.auditId) {
              <li class="hist__audit" [class.hist__audit--muted]="a.status === 'cancelled'">
                <div class="hist__audit-head">
                  @if (canOpenAudits()) {
                    <a class="hist__audit-name" [routerLink]="['/audits', a.auditId]" mat-dialog-close>{{ a.name }}</a>
                  } @else {
                    <span class="hist__audit-name">{{ a.name }}</span>
                  }
                  <span class="status-badge" [attr.data-status]="a.status">{{ statusKey(a.status) | t }}</span>
                </div>

                <p class="hist__meta">
                  <span class="hist__chip" [attr.data-origin]="a.origin">{{ originLabel(a) }}</span>
                  <span>{{ auditTypeLabel(a.auditType) }}</span>
                  <span aria-hidden="true">·</span>
                  <span>
                    {{ a.startDate | date: 'mediumDate' }} –
                    @if (a.actualEndDate) {
                      {{ a.actualEndDate | date: 'mediumDate' }}
                    } @else {
                      {{ a.targetEndDate | date: 'mediumDate' }} ({{ 'universe.history.target' | t }})
                    }
                  </span>
                </p>

                <dl class="hist__facts">
                  <div>
                    <dt>{{ 'audits.field.lead' | t }}</dt>
                    <dd>{{ a.leadName ?? '—' }}</dd>
                  </div>
                  @if (!a.isSelfAssessment) {
                    <div>
                      <dt>{{ 'audits.field.auditee' | t }}</dt>
                      <dd>{{ a.auditeeName ?? '—' }}</dd>
                    </div>
                  }
                  <div>
                    <dt>{{ 'universe.history.checklist' | t }}</dt>
                    <dd>
                      @if (a.checklistItems === 0) {
                        —
                      } @else {
                        {{ 'universe.history.checklistValue' | t: { responded: a.respondedItems, total: a.checklistItems } }}
                        <span class="hist__verdicts">
                          <span class="hist__v hist__v--pass">{{ 'universe.history.pass' | t: { count: a.passed } }}</span>
                          <span class="hist__v hist__v--fail">{{ 'universe.history.fail' | t: { count: a.failed } }}</span>
                          @if (a.notApplicable > 0) {
                            <span class="hist__v">{{ 'universe.history.na' | t: { count: a.notApplicable } }}</span>
                          }
                        </span>
                      }
                    </dd>
                  </div>
                  <div>
                    <dt>{{ 'universe.history.findings' | t }}</dt>
                    <dd>
                      @if (a.findings === 0) {
                        {{ 'universe.history.noFindings' | t }}
                      } @else {
                        {{ 'universe.history.findingsValue' | t: { count: a.findings, open: a.openFindings } }}
                        <span class="hist__verdicts">
                          @for (s of severities(a); track s.key) {
                            <span class="hist__sev" [attr.data-severity]="s.key">{{ s.count }} {{ s.labelKey | t }}</span>
                          }
                        </span>
                      }
                    </dd>
                  </div>
                  <div>
                    <dt>{{ 'universe.history.reports' | t }}</dt>
                    <dd>{{ a.reportsIssued > 0 ? a.reportsIssued : '—' }}</dd>
                  </div>
                </dl>
              </li>
            }
          </ol>
        }
      }
    </mat-dialog-content>

    <mat-dialog-actions align="end">
      <button matButton type="button" mat-dialog-close>{{ 'universe.actions.close' | t }}</button>
    </mat-dialog-actions>
  `,
  styles: `
    .hist__title {
      display: flex;
      flex-direction: column;
      gap: 0.15rem;
    }
    .hist__entity {
      font: var(--mat-sys-body-medium);
      color: var(--mat-sys-on-surface-variant);
    }
    .hist {
      min-width: min(820px, 86vw);
    }
    .hist__stats {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(128px, 1fr));
      gap: 0.75rem;
      margin: 0 0 1rem;
    }
    .hist__stat {
      padding: 0.7rem 0.85rem;
      border: 1px solid var(--mat-sys-outline-variant);
      border-radius: 12px;
      background: var(--mat-sys-surface-container-low);
    }
    .hist__stat dt,
    .hist__facts dt {
      font: var(--mat-sys-label-small);
      letter-spacing: 0.04em;
      text-transform: uppercase;
      color: var(--mat-sys-on-surface-variant);
    }
    .hist__stat dd {
      margin: 0.2rem 0 0;
      font: var(--mat-sys-title-medium);
      font-variant-numeric: tabular-nums;
    }
    .hist__of {
      font: var(--mat-sys-body-small);
      color: var(--mat-sys-on-surface-variant);
    }
    .hist__warn {
      color: var(--mat-sys-error);
    }
    .hist__note {
      display: flex;
      align-items: center;
      gap: 0.5rem;
      margin: 0 0 1rem;
      font: var(--mat-sys-body-small);
      color: var(--mat-sys-on-surface-variant);
    }
    .hist__list {
      list-style: none;
      margin: 0;
      padding: 0;
      display: flex;
      flex-direction: column;
      gap: 0.75rem;
    }
    .hist__audit {
      padding: 0.9rem 1rem;
      border: 1px solid var(--mat-sys-outline-variant);
      border-radius: 12px;
    }
    .hist__audit--muted {
      opacity: 0.7;
    }
    .hist__audit-head {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 0.75rem;
    }
    .hist__audit-name {
      font: var(--mat-sys-title-small);
      color: var(--mat-sys-on-surface);
      overflow-wrap: anywhere;
    }
    a.hist__audit-name {
      color: var(--mat-sys-primary);
      text-decoration: none;
    }
    a.hist__audit-name:hover,
    a.hist__audit-name:focus-visible {
      text-decoration: underline;
    }
    .hist__meta {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: 0.4rem;
      margin: 0.35rem 0 0.7rem;
      font: var(--mat-sys-body-small);
      color: var(--mat-sys-on-surface-variant);
    }
    .hist__chip {
      padding: 0.1rem 0.5rem;
      border-radius: 999px;
      background: var(--mat-sys-secondary-container);
      color: var(--mat-sys-on-secondary-container);
      font: var(--mat-sys-label-small);
    }
    .hist__chip[data-origin='ad_hoc'] {
      background: var(--mat-sys-tertiary-container);
      color: var(--mat-sys-on-tertiary-container);
    }
    .hist__chip[data-origin='self_assessment'] {
      background: var(--mat-sys-surface-container-highest);
      color: var(--mat-sys-on-surface-variant);
    }
    .hist__facts {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(150px, 1fr));
      gap: 0.6rem 1rem;
      margin: 0;
    }
    .hist__facts dd {
      margin: 0.15rem 0 0;
      font: var(--mat-sys-body-medium);
    }
    .hist__verdicts {
      display: flex;
      flex-wrap: wrap;
      gap: 0.3rem 0.6rem;
      margin-top: 0.15rem;
      font: var(--mat-sys-body-small);
      color: var(--mat-sys-on-surface-variant);
    }
    .hist__v--fail,
    .hist__sev[data-severity='critical'],
    .hist__sev[data-severity='high'] {
      color: var(--mat-sys-error);
    }
    .hist__empty {
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: 0.25rem;
      padding: 1.75rem 1rem;
      text-align: center;
      color: var(--mat-sys-on-surface-variant);
    }
    .hist__empty p {
      margin: 0;
    }
    .hist__empty-hint {
      font: var(--mat-sys-body-small);
      max-width: 46ch;
    }
    @media (max-width: 640px) {
      .hist {
        min-width: auto;
      }
    }
  `,
})
export class EntityAuditHistoryDialogComponent {
  readonly data = inject<EntityAuditHistoryDialogData>(MAT_DIALOG_DATA);
  private readonly universe = inject(UniverseService);
  private readonly auth = inject(AuthService);
  private readonly refLookup = inject(ReferenceDataLookupService);
  private readonly i18n = inject(TranslationService);

  readonly loading = signal(true);
  readonly failed = signal(false);
  readonly history = signal<EntityAuditHistory | null>(null);

  /** Audit names link to the audit only for someone who can open audits. */
  readonly canOpenAudits = computed(() => this.auth.hasPermission(Permissions.ViewAudit));

  private readonly auditTypes = this.refLookup.options('audit_type');

  constructor() {
    this.universe.auditHistory(this.data.entityId).subscribe({
      next: (history) => {
        this.history.set(history);
        this.loading.set(false);
      },
      error: () => {
        this.failed.set(true);
        this.loading.set(false);
      },
    });
  }

  statusKey(status: string): string {
    return STATUS_KEYS[status] ?? status;
  }

  /** Reference-data label for an audit type code, falling back to the code itself. */
  auditTypeLabel(code: string): string {
    return this.auditTypes().find((o) => o.code === code)?.label ?? code;
  }

  /** "Annual plan · 2026", "Ad hoc" or "Self-assessment" — where the audit came from. */
  originLabel(a: EntityAuditHistoryItem): string {
    switch (a.origin) {
      case 'plan':
        return a.planPeriodLabel
          ? this.translate('universe.history.origin.planPeriod', { period: a.planPeriodLabel })
          : this.translate('universe.history.origin.plan');
      case 'self_assessment':
        return this.translate('universe.history.origin.selfAssessment');
      default:
        return this.translate('universe.history.origin.adHoc');
    }
  }

  /** Non-zero finding counts by severity, most severe first. */
  severities(a: EntityAuditHistoryItem): { key: string; count: number; labelKey: string }[] {
    return [
      { key: 'critical', count: a.criticalFindings, labelKey: 'universe.history.sev.critical' },
      { key: 'high', count: a.highFindings, labelKey: 'universe.history.sev.high' },
      { key: 'medium', count: a.mediumFindings, labelKey: 'universe.history.sev.medium' },
      { key: 'low', count: a.lowFindings, labelKey: 'universe.history.sev.low' },
    ].filter((s) => s.count > 0);
  }

  private translate(key: string, params?: Record<string, string | number>): string {
    return this.i18n.translate(key, params);
  }
}
