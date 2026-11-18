import { ChangeDetectionStrategy, Component, computed, inject, input, output, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatProgressBarModule } from '@angular/material/progress-bar';

import { IconComponent } from '../../../core/icons/icon.component';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';
import { ChecklistProgressItem } from '../../../core/models';
import { ReviewNoteListComponent } from './review-note-list.component';
import { WorkingPaperReviewStore } from './working-paper-review.store';

/**
 * The audit's working-paper review at a glance: how many items are signed off, the notes still needing attention
 * (each linking to its item), notes on the audit as a whole, and — while the audit is under review — why it cannot
 * be completed yet. Hidden for a self-assessment, which has no independent review.
 */
@Component({
  selector: 'app-audit-review-panel',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatCardModule, MatButtonModule, MatProgressBarModule, IconComponent, TranslatePipe, ReviewNoteListComponent],
  template: `
    @if (review(); as r) {
      <mat-card appearance="outlined" class="review">
        <div class="review__head">
          <h3 class="review__title">
            <app-icon name="rate_review" aria-hidden="true" />
            {{ 'audits.review.title' | t }}
          </h3>
          @if (r.canReview && r.isOpen) {
            <button matButton="tonal" type="button" [disabled]="store.busy()" (click)="store.raise(null, null)">
              <app-icon name="note_add" />
              {{ 'audits.review.action.raiseOnAudit' | t }}
            </button>
          }
        </div>

        <div class="review__progress">
          <div class="review__progress-head">
            <span>{{ 'audits.review.signedOff' | t: { signed: r.signedOffItems, total: r.totalItems } }}</span>
            <span class="review__pct">{{ pct() }}%</span>
          </div>
          <mat-progress-bar mode="determinate" [value]="pct()" />
          <p class="review__meta">
            {{ 'audits.review.meta' | t: { ready: r.readyForSignOff, open: r.openNotes, answered: r.answeredNotes } }}
          </p>
        </div>

        @if (status() === 'under_review') {
          @if (complete()) {
            <p class="review__callout review__callout--done" role="status">
              <app-icon name="verified" aria-hidden="true" />
              <span>{{ 'audits.review.readyToComplete' | t }}</span>
            </p>
          } @else {
            <p class="review__callout" role="note">
              <app-icon name="lock" aria-hidden="true" />
              <span>{{ 'audits.review.completeBlocked' | t: { items: r.totalItems - r.signedOffItems, notes: r.openNotes + r.answeredNotes } }}</span>
            </p>
          }
        }

        @if (r.isOpen) {
          <p class="review__role">
            {{ (r.canReview ? 'audits.review.roleReviewer' : r.canAnswer ? 'audits.review.roleTeam' : 'audits.review.roleViewer') | t }}
          </p>
        }

        @if (outstanding().length) {
          <h4 class="review__subtitle">{{ 'audits.review.outstandingTitle' | t: { count: outstanding().length } }}</h4>
          <app-review-note-list [notes]="outstanding()" [itemPrompts]="prompts()" (openItem)="openItem.emit($event)" />
        } @else if (r.notes.length === 0) {
          <p class="review__empty">{{ 'audits.review.noNotes' | t }}</p>
        }

        @if (cleared().length) {
          <button matButton type="button" class="review__toggle" (click)="showCleared.set(!showCleared())"
            [attr.aria-expanded]="showCleared()">
            <app-icon [name]="showCleared() ? 'expand_less' : 'expand_more'" />
            {{ (showCleared() ? 'audits.review.hideCleared' : 'audits.review.showCleared') | t: { count: cleared().length } }}
          </button>
          @if (showCleared()) {
            <app-review-note-list [notes]="cleared()" [itemPrompts]="prompts()" (openItem)="openItem.emit($event)" />
          }
        }
      </mat-card>
    }
  `,
  styles: `
    .review {
      display: flex;
      flex-direction: column;
      gap: 12px;
      padding: 16px 20px;
    }
    .review__head {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      justify-content: space-between;
      gap: 8px 16px;
    }
    .review__title {
      display: flex;
      align-items: center;
      gap: 8px;
      margin: 0;
      font-size: 1rem;
      font-weight: 600;
    }
    .review__progress {
      display: flex;
      flex-direction: column;
      gap: 6px;
    }
    .review__progress-head {
      display: flex;
      justify-content: space-between;
      gap: 8px;
      font-size: 0.9rem;
    }
    .review__pct {
      color: var(--mat-sys-on-surface-variant);
      font-variant-numeric: tabular-nums;
    }
    .review__meta,
    .review__role,
    .review__empty {
      margin: 0;
      color: var(--mat-sys-on-surface-variant);
      font-size: 0.85rem;
      line-height: 1.45;
    }
    .review__callout {
      display: flex;
      align-items: flex-start;
      gap: 8px;
      margin: 0;
      padding: 10px 12px;
      border-radius: 10px;
      background: color-mix(in srgb, #ef6c00 10%, var(--mat-sys-surface));
      color: var(--mat-sys-on-surface);
      font-size: 0.875rem;
      line-height: 1.45;
    }
    .review__callout--done {
      background: color-mix(in srgb, #2e7d32 12%, var(--mat-sys-surface));
    }
    .review__callout app-icon {
      flex: 0 0 auto;
    }
    .review__subtitle {
      margin: 4px 0 0;
      font-size: 0.85rem;
      font-weight: 600;
      letter-spacing: 0.02em;
      text-transform: uppercase;
      color: var(--mat-sys-on-surface-variant);
    }
    .review__toggle {
      align-self: flex-start;
    }
  `,
})
export class AuditReviewPanelComponent {
  readonly store = inject(WorkingPaperReviewStore);

  /** The checklist (for each note's item prompt). */
  readonly items = input.required<ChecklistProgressItem[]>();
  readonly status = input.required<string>();

  /** A note's item was clicked: open it in the workspace. */
  readonly openItem = output<string>();

  readonly showCleared = signal(false);

  /** The review, when there is one to show (a self-assessment has none). */
  readonly review = computed(() => {
    const r = this.store.review();
    return r && (r.isRequired || r.notes.length > 0) ? r : null;
  });

  readonly pct = computed(() => {
    const r = this.review();
    return r && r.totalItems > 0 ? Math.round((r.signedOffItems / r.totalItems) * 100) : 0;
  });

  readonly complete = computed(() => this.store.isComplete(this.review()));

  readonly prompts = computed<Record<string, string>>(() =>
    Object.fromEntries(this.items().map((i) => [i.itemId, i.prompt])),
  );

  readonly outstanding = computed(() => (this.review()?.notes ?? []).filter((n) => n.status !== 'cleared'));
  readonly cleared = computed(() => (this.review()?.notes ?? []).filter((n) => n.status === 'cleared'));
}
