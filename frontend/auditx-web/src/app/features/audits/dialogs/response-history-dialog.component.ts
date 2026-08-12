import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { MatButtonModule } from '@angular/material/button';
import {
  MAT_DIALOG_DATA,
  MatDialogModule,
  MatDialogRef,
} from '@angular/material/dialog';

import { AuditsService } from '../../../core/services/audits.service';
import { UserLookupService } from '../../../core/services/user-lookup.service';
import { ResponseHistoryEntry } from '../../../core/models';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';
import { TranslationService } from '../../../core/i18n/translation.service';

export interface ResponseHistoryDialogData {
  auditId: string;
  itemId: string;
  prompt: string;
}

type LoadState = 'loading' | 'ready' | 'error';

/** The verdict/comment/value/score snapshot recorded on a response event (see ResponseCommands.Snapshot on the backend). */
interface ResponseStateSnapshot {
  verdict?: 'pass' | 'fail' | 'na' | null;
  comment?: string | null;
  valueJson?: string | null;
  isDraft?: boolean;
  score?: number | null;
}

/** A single before/after field the timeline compares: "was" is null when there's nothing to compare against (the entry that first recorded the response). */
interface FieldDiff {
  label: string;
  was: string | null;
  now: string;
  changed: boolean;
}

/** A history entry with its before/after snapshots pre-parsed and diffed into a display-ready row. */
interface HistoryRow {
  entry: ResponseHistoryEntry;
  after: ResponseStateSnapshot | null;
  diffs: FieldDiff[];
}

/**
 * Full before/after timeline of the lifecycle events recorded against a response: what it was, what it became,
 * who changed it and when — not just the latest state. Every edit (not only the first response) shows a
 * decision/value/comment/score comparison so a reviewer can see exactly what moved.
 */
@Component({
  selector: 'app-response-history-dialog',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DatePipe, MatDialogModule, MatButtonModule, TranslatePipe],
  template: `
    <h2 mat-dialog-title>{{ 'audits.history.title' | t }}</h2>
    <mat-dialog-content>
      <p class="prompt">{{ data.prompt }}</p>
      @switch (state()) {
        @case ('loading') {
          <p class="muted">{{ 'audits.history.loading' | t }}</p>
        }
        @case ('error') {
          <p class="muted">{{ 'audits.history.error' | t }}</p>
        }
        @case ('ready') {
          @if (!rows().length) {
            <p class="muted">{{ 'audits.history.empty' | t }}</p>
          } @else {
            <ol class="timeline">
              @for (row of rows(); track row.entry.id) {
                <li class="timeline__entry">
                  <div class="timeline__dot" aria-hidden="true"></div>
                  <div class="timeline__body">
                    <div class="timeline__head">
                      <span class="timeline__event">{{ eventLabel(row.entry.eventType) }}</span>
                      @if (row.after?.verdict) {
                        <span class="verdict-chip" [attr.data-verdict]="row.after!.verdict">
                          {{ verdictLabel(row.after!.verdict) }}
                        </span>
                      }
                      @if (row.after?.isDraft) {
                        <span class="verdict-chip" data-verdict="draft">{{ 'audits.verdict.draft' | t }}</span>
                      }
                      @if (row.after?.score !== null && row.after?.score !== undefined) {
                        <span class="score-chip">{{ 'audits.history.score' | t: { score: row.after!.score } }}</span>
                      }
                    </div>
                    @if (row.diffs.length) {
                      <dl class="timeline__diff">
                        @for (d of row.diffs; track d.label) {
                          <div class="diff-row">
                            <dt class="diff-row__label">{{ d.label }}</dt>
                            <dd class="diff-row__value">
                              @if (d.was !== null && d.changed) {
                                <span class="diff-row__was">{{ d.was }}</span>
                                <span class="diff-row__arrow" aria-hidden="true">→</span>
                              }
                              <span class="diff-row__now">{{ d.now }}</span>
                            </dd>
                          </div>
                        }
                      </dl>
                    }
                    <span class="muted timeline__meta">
                      {{ nameOf(row.entry.actorUserId) }} ·
                      {{ row.entry.occurredAtUtc | date: 'medium' }}
                    </span>
                  </div>
                </li>
              }
            </ol>
          }
        }
      }
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button matButton type="button" (click)="close()">{{ 'audits.history.close' | t }}</button>
    </mat-dialog-actions>
  `,
  styles: `
    .prompt {
      margin: 0 0 1rem;
      font-weight: 500;
    }
    .muted {
      color: var(--mat-sys-on-surface-variant);
    }
    .timeline {
      list-style: none;
      margin: 0;
      padding: 0;
      min-width: 460px;
      display: flex;
      flex-direction: column;
      gap: 0.75rem;
    }
    .timeline__entry {
      display: flex;
      gap: 0.75rem;
      align-items: flex-start;
    }
    .timeline__dot {
      margin-top: 0.4rem;
      width: 0.6rem;
      height: 0.6rem;
      border-radius: 50%;
      background: var(--mat-sys-primary);
      flex: 0 0 auto;
    }
    .timeline__body {
      display: flex;
      flex-direction: column;
      gap: 0.3rem;
      min-width: 0;
      flex: 1;
    }
    .timeline__head {
      display: flex;
      align-items: center;
      flex-wrap: wrap;
      gap: 0.4rem;
    }
    .timeline__event {
      font-weight: 500;
    }
    .timeline__meta {
      font: var(--mat-sys-label-small);
    }
    .timeline__diff {
      margin: 0.15rem 0 0;
      display: flex;
      flex-direction: column;
      gap: 0.2rem;
      padding: 0.5rem 0.65rem;
      border-radius: 8px;
      background: var(--mat-sys-surface-container-low);
    }
    .diff-row {
      display: flex;
      gap: 0.5rem;
      align-items: baseline;
      font-size: 0.8125rem;
    }
    .diff-row__label {
      flex: 0 0 auto;
      min-width: 5.5rem;
      color: var(--mat-sys-on-surface-variant);
      font-weight: 500;
    }
    .diff-row__value {
      margin: 0;
      display: flex;
      gap: 0.4rem;
      align-items: baseline;
      flex-wrap: wrap;
      overflow-wrap: anywhere;
    }
    .diff-row__was {
      color: var(--mat-sys-on-surface-variant);
      text-decoration: line-through;
      text-decoration-color: var(--mat-sys-outline-variant);
    }
    .diff-row__arrow {
      color: var(--mat-sys-on-surface-variant);
    }
    .diff-row__now {
      color: var(--mat-sys-on-surface);
      font-weight: 500;
    }
    .verdict-chip {
      padding: 0.1rem 0.5rem;
      border-radius: 999px;
      font: var(--mat-sys-label-small);
      text-transform: uppercase;
      letter-spacing: 0.02em;
      background: var(--mat-sys-surface-container-high);
      color: var(--mat-sys-on-surface);
    }
    .verdict-chip[data-verdict='pass'] {
      background: color-mix(in srgb, var(--mat-sys-primary) 18%, transparent);
      color: var(--mat-sys-primary);
    }
    .verdict-chip[data-verdict='fail'] {
      background: color-mix(in srgb, var(--mat-sys-error) 18%, transparent);
      color: var(--mat-sys-error);
    }
    .verdict-chip[data-verdict='na'],
    .verdict-chip[data-verdict='draft'] {
      background: var(--mat-sys-surface-container-high);
      color: var(--mat-sys-on-surface-variant);
    }
    .score-chip {
      padding: 0.1rem 0.5rem;
      border-radius: 999px;
      font: var(--mat-sys-label-small);
      background: var(--mat-sys-secondary-container);
      color: var(--mat-sys-on-secondary-container);
    }
    @media (max-width: 520px) {
      .timeline {
        min-width: auto;
      }
    }
  `,
})
export class ResponseHistoryDialogComponent {
  readonly data = inject<ResponseHistoryDialogData>(MAT_DIALOG_DATA);
  private readonly dialogRef =
    inject<MatDialogRef<ResponseHistoryDialogComponent>>(MatDialogRef);
  private readonly service = inject(AuditsService);
  private readonly userLookup = inject(UserLookupService);
  private readonly i18n = inject(TranslationService);

  readonly state = signal<LoadState>('loading');
  readonly rows = signal<HistoryRow[]>([]);

  constructor() {
    this.service
      .getResponseHistory(this.data.auditId, this.data.itemId)
      .subscribe({
        next: (entries) => {
          this.rows.set(entries.map((entry) => this.toRow(entry)));
          this.state.set('ready');
        },
        error: () => this.state.set('error'),
      });
  }

  /** Specific label for known event types; falls back to a humanised version of the raw event type. */
  eventLabel(eventType: string): string {
    const key = `audits.history.event.${eventType}`;
    const label = this.i18n.translate(key);
    return label === key ? eventType.replace(/_/g, ' ') : label;
  }

  verdictLabel(verdict: string | null | undefined): string {
    if (!verdict) {
      return this.i18n.translate('audits.history.none');
    }
    const key = `audits.verdict.${verdict}`;
    const label = this.i18n.translate(key);
    return label === key ? verdict : label;
  }

  nameOf(userId: string | null | undefined): string {
    // Automated/system events carry no actor id; an unresolved actor (deleted user) also reads as "System".
    if (!userId) {
      return this.i18n.translate('audits.history.system');
    }
    const name = this.userLookup.displayName(userId);
    return name === userId ? this.i18n.translate('audits.history.system') : name;
  }

  close(): void {
    this.dialogRef.close();
  }

  private toRow(entry: ResponseHistoryEntry): HistoryRow {
    const before = this.parseState(entry.beforeStateJson);
    const after = this.parseState(entry.stateJson);
    return { entry, after, diffs: this.buildDiffs(before, after) };
  }

  /**
   * Field-by-field comparison so a reviewer sees exactly what changed, not just the resulting state: the
   * decision (verdict), the captured value (for value-type items), the comment, and the score. A field is
   * included whenever the "after" state carries it; "was" stays null (no strikethrough/arrow) when there's no
   * prior state to compare against or the field is unchanged.
   */
  private buildDiffs(before: ResponseStateSnapshot | null, after: ResponseStateSnapshot | null): FieldDiff[] {
    if (!after) {
      return [];
    }

    const diffs: FieldDiff[] = [];

    const decisionNow = this.verdictLabel(after.verdict);
    const decisionWas = before ? this.verdictLabel(before.verdict) : null;
    if (after.verdict || decisionWas) {
      diffs.push(this.diff(this.i18n.translate('audits.history.decision'), decisionWas, decisionNow));
    }

    const valueNow = this.describeValue(after.valueJson);
    const valueWas = before ? this.describeValue(before.valueJson) : null;
    if (valueNow || valueWas) {
      diffs.push(this.diff(this.i18n.translate('audits.history.value'), valueWas, valueNow ?? this.i18n.translate('audits.history.none')));
    }

    const commentNow = after.comment?.trim() || this.i18n.translate('audits.history.none');
    const commentWas = before ? before.comment?.trim() || this.i18n.translate('audits.history.none') : null;
    if (after.comment || commentWas) {
      diffs.push(this.diff(this.i18n.translate('audits.history.comment'), commentWas, commentNow));
    }

    if (after.score !== null && after.score !== undefined) {
      const scoreWas = before?.score !== null && before?.score !== undefined ? String(before.score) : null;
      diffs.push(this.diff(this.i18n.translate('audits.history.scoreLabel'), scoreWas, String(after.score)));
    }

    return diffs;
  }

  private diff(label: string, was: string | null, now: string): FieldDiff {
    return { label, was, now, changed: was !== null && was !== now };
  }

  /** Extract the type-specific scalar from a captured value JSON (e.g. {"rating":4} -> "4"), for display only. */
  private describeValue(valueJson: string | null | undefined): string | null {
    if (!valueJson) {
      return null;
    }
    try {
      const v = JSON.parse(valueJson) as Record<string, unknown>;
      const raw = v['text'] ?? v['number'] ?? v['date'] ?? v['rating'] ?? v['choice'];
      return raw === undefined || raw === null ? null : String(raw);
    } catch {
      return null;
    }
  }

  private parseState(stateJson: string | null | undefined): ResponseStateSnapshot | null {
    if (!stateJson) {
      return null;
    }
    try {
      return JSON.parse(stateJson) as ResponseStateSnapshot;
    } catch {
      return null;
    }
  }
}
