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

/** The verdict/comment/score snapshot recorded on a response event (see ResponseCommands.Snapshot on the backend). */
interface ResponseStateSnapshot {
  verdict?: 'pass' | 'fail' | 'na' | null;
  comment?: string | null;
  valueJson?: string | null;
  isDraft?: boolean;
  score?: number | null;
}

/** A history entry with its state snapshot pre-parsed (null when the entry carries no/unparseable state). */
interface HistoryRow {
  entry: ResponseHistoryEntry;
  state: ResponseStateSnapshot | null;
}

/** Read-only timeline of the lifecycle events recorded against a response — what was actually done at each step, not just the event name. */
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
                      @if (row.state?.verdict) {
                        <span class="verdict-chip" [attr.data-verdict]="row.state!.verdict">
                          {{ verdictLabel(row.state!.verdict) }}
                        </span>
                      }
                      @if (row.state?.isDraft) {
                        <span class="verdict-chip" data-verdict="draft">{{ 'audits.verdict.draft' | t }}</span>
                      }
                      @if (row.state?.score !== null && row.state?.score !== undefined) {
                        <span class="score-chip">{{ 'audits.history.score' | t: { score: row.state!.score } }}</span>
                      }
                    </div>
                    @if (row.state?.comment) {
                      <p class="timeline__comment">{{ row.state!.comment }}</p>
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
      min-width: 420px;
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
      gap: 0.15rem;
      min-width: 0;
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
    .timeline__comment {
      margin: 0.1rem 0 0;
      white-space: pre-wrap;
      overflow-wrap: anywhere;
    }
    .timeline__meta {
      font: var(--mat-sys-label-small);
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
          this.rows.set(entries.map((entry) => ({ entry, state: this.parseState(entry.stateJson) })));
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
      return '';
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
