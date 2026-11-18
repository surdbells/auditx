import { ChangeDetectionStrategy, Component, computed, inject, input, signal } from '@angular/core';
import { DatePipe, formatDate } from '@angular/common';
import { LOCALE_ID } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';

import { IconComponent } from '../../../core/icons/icon.component';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';
import { TranslationService } from '../../../core/i18n/translation.service';
import { UserLookupService } from '../../../core/services/user-lookup.service';
import { Audit, EngagementLetterStatus } from '../../../core/models';
import { EngagementRecordsStore } from './engagement-records.store';

/** Audit states in which the letter is written and issued (the server's rule). */
const LETTER_OPEN = new Set(['draft', 'planned', 'in_progress']);

const TONE: Record<EngagementLetterStatus, string> = { draft: 'pending', issued: 'info', acknowledged: 'done' };

/**
 * The engagement letter: what the team tells the area about the audit before it starts. The team writes it and issues
 * it; the auditee reads and acknowledges it. A letter changed after it was issued becomes the next revision; the earlier
 * ones stay readable below it.
 */
@Component({
  selector: 'app-engagement-letter-card',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DatePipe, MatCardModule, MatButtonModule, IconComponent, TranslatePipe],
  styleUrl: './engagement-card.scss',
  template: `
    @if (store.records(); as r) {
      <mat-card appearance="outlined" class="eng" data-guide="engagement-letter">
        <div class="eng__head">
          <h3 class="eng__title">
            <app-icon name="mail" />
            {{ 'engagement.letter.title' | t }}
            @if (r.letter; as l) {
              <span class="eng__chip" [attr.data-tone]="tone(l.status)">{{ 'engagement.letter.status.' + l.status | t }}</span>
              @if (l.revision > 1) {
                <span class="eng__muted">{{ 'engagement.letter.revision' | t: { n: l.revision } }}</span>
              }
            }
          </h3>
          <div class="eng__actions">
            @if (canWrite()) {
              <button matButton="tonal" type="button" [disabled]="store.busy()" (click)="write()">
                <app-icon name="edit" />
                {{ (r.letter ? (r.letter.status === 'draft' ? 'engagement.letter.action.edit' : 'engagement.letter.action.revise') : 'engagement.letter.action.write') | t }}
              </button>
              @if (r.letter?.status === 'draft') {
                <button matButton="filled" type="button" [disabled]="store.busy()" (click)="store.issueLetter(nameOf(audit().auditeeUserId))">
                  <app-icon name="send" />
                  {{ 'engagement.letter.action.issue' | t }}
                </button>
              }
            }
            @if (r.canAcknowledge) {
              <button matButton="filled" type="button" [disabled]="store.busy()" (click)="store.acknowledgeLetter()">
                <app-icon name="task_alt" />
                {{ 'engagement.letter.action.acknowledge' | t }}
              </button>
            }
          </div>
        </div>

        @if (r.letter; as l) {
          <div class="eng__stack">
            @if (r.canAcknowledge) {
              <p class="eng__lead">{{ 'engagement.letter.acknowledgePrompt' | t }}</p>
            } @else if (l.status === 'issued' && !r.isAuditee) {
              <p class="eng__lead">{{ 'engagement.letter.awaiting' | t: { name: nameOf(audit().auditeeUserId) } }}</p>
            }
            <p class="eng__text">{{ l.body }}</p>
            <p class="eng__meta">
              @if (l.issuedAt) {
                <span>{{ 'engagement.letter.issuedBy' | t: { name: nameOf(l.issuedBy), date: (l.issuedAt | date: 'medium') } }}</span>
              } @else {
                <span>{{ 'engagement.letter.draftedBy' | t: { name: nameOf(l.draftedBy), date: (l.draftedAt | date: 'medium') } }}</span>
              }
              @if (l.acknowledgedAt) {
                <span>·</span>
                <span>{{ 'engagement.letter.acknowledgedBy' | t: { name: nameOf(l.acknowledgedBy), date: (l.acknowledgedAt | date: 'medium') } }}</span>
              }
            </p>
            @if (l.acknowledgementComment) {
              <blockquote class="eng__quote">{{ l.acknowledgementComment }}</blockquote>
            }

            @if (l.previousRevisions.length) {
              <button matButton type="button" class="eng__toggle" (click)="showHistory.set(!showHistory())">
                <app-icon [name]="showHistory() ? 'expand_less' : 'expand_more'" />
                {{ (showHistory() ? 'engagement.letter.hideRevisions' : 'engagement.letter.showRevisions') | t: { count: l.previousRevisions.length } }}
              </button>
              @if (showHistory()) {
                <ul class="eng__rows">
                  @for (p of l.previousRevisions; track p.revision) {
                    <li class="eng__row">
                      <div class="eng__row-head">
                        <span class="eng__row-name">{{ 'engagement.letter.revision' | t: { n: p.revision } }}</span>
                        @if (p.issuedAt) {
                          <span class="eng__meta">{{ 'engagement.letter.issuedBy' | t: { name: nameOf(p.issuedBy), date: (p.issuedAt | date: 'medium') } }}</span>
                        }
                        @if (p.acknowledgedAt) {
                          <span class="eng__meta">· {{ 'engagement.letter.acknowledgedBy' | t: { name: nameOf(p.acknowledgedBy), date: (p.acknowledgedAt | date: 'medium') } }}</span>
                        } @else {
                          <span class="eng__meta">· {{ 'engagement.letter.notAcknowledged' | t }}</span>
                        }
                      </div>
                      <p class="eng__text">{{ p.body }}</p>
                    </li>
                  }
                </ul>
              }
            }
          </div>
        } @else {
          <p class="eng__muted">
            {{ (canWrite() ? 'engagement.letter.emptyTeam' : r.isAuditee ? 'engagement.letter.emptyAuditee' : 'engagement.letter.empty') | t }}
          </p>
        }
      </mat-card>
    }
  `,
})
export class EngagementLetterCardComponent {
  readonly store = inject(EngagementRecordsStore);
  private readonly users = inject(UserLookupService);
  private readonly i18n = inject(TranslationService);
  private readonly locale = inject(LOCALE_ID);

  readonly audit = input.required<Audit>();

  readonly showHistory = signal(false);

  /** The team writes and issues the letter before and during fieldwork. */
  readonly canWrite = computed(() => !!this.store.records()?.canManage && LETTER_OPEN.has(this.audit().status));

  tone(status: EngagementLetterStatus): string {
    return TONE[status];
  }

  nameOf(userId: string | null | undefined): string {
    return this.users.displayName(userId);
  }

  write(): void {
    this.store.writeLetter(this.startingText());
  }

  /** A first draft from what the audit already says: its name, dates, scope and lead. */
  private startingText(): string {
    const a = this.audit();
    const date = (value: string | null) => (value ? formatDate(value, 'longDate', this.locale) : '—');
    return this.i18n.translate('engagement.letter.template', {
      name: a.name,
      auditee: this.nameOf(a.auditeeUserId),
      start: date(a.startDate),
      end: date(a.targetEndDate),
      scope: a.scopeDescription?.trim() || this.i18n.translate('engagement.letter.templateScopeTbc'),
      lead: this.nameOf(a.leadUserId),
    });
  }
}
