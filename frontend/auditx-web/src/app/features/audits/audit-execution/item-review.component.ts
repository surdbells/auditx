import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { DatePipe } from '@angular/common';
import { MatButtonModule } from '@angular/material/button';

import { IconComponent } from '../../../core/icons/icon.component';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';
import { AuthService } from '../../../core/services/auth.service';
import { UserLookupService } from '../../../core/services/user-lookup.service';
import { ChecklistProgressItem } from '../../../core/models';
import { ReviewNoteListComponent } from './review-note-list.component';
import { WorkingPaperReviewStore } from './working-paper-review.store';

/**
 * The open checklist item's review: whether it is signed off and by whom, the reviewer's Sign off / Raise note
 * actions, and the notes on it. Mirrors the server's rules, so it never offers what the API would refuse — and says
 * why when the reviewer cannot sign off (they answered the item, or a note is outstanding).
 */
@Component({
  selector: 'app-item-review',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DatePipe, MatButtonModule, IconComponent, TranslatePipe, ReviewNoteListComponent],
  template: `
    @if (state(); as s) {
      <section class="item-review" [attr.aria-label]="'audits.review.itemTitle' | t">
        <div class="item-review__head">
          <span class="item-review__title">{{ 'audits.review.itemTitle' | t }}</span>
          @if (s.reviewedBy) {
            <span class="item-review__state item-review__state--done">
              <app-icon name="verified" aria-hidden="true" />
              {{ 'audits.review.itemSignedOff' | t: { name: nameOf(s.reviewedBy), date: (s.reviewedAt | date: 'medium') } }}
            </span>
          } @else if (!s.isAnswered) {
            <span class="item-review__state">{{ 'audits.review.itemNotAnswered' | t }}</span>
          } @else if (s.outstandingNotes > 0) {
            <span class="item-review__state">{{ 'audits.review.itemWaitingOnNotes' | t: { count: s.outstandingNotes } }}</span>
          } @else {
            <span class="item-review__state">{{ 'audits.review.itemAwaitingSignOff' | t }}</span>
          }
        </div>

        @if (canAct()) {
          @if (answeredByMe() && !s.reviewedBy) {
            <p class="item-review__hint" role="note">
              <app-icon name="lock" aria-hidden="true" />
              <span>{{ 'audits.review.ownWork' | t }}</span>
            </p>
          }
          <div class="item-review__actions">
            @if (!s.reviewedBy) {
              <button matButton="filled" type="button" [disabled]="!canSignOff() || store.busy()" (click)="store.signOff(item().itemId)">
                <app-icon name="verified" />
                {{ 'audits.review.action.signOff' | t }}
              </button>
            }
            <button matButton="tonal" type="button" [disabled]="store.busy()" (click)="store.raise(item().itemId, item().prompt)">
              <app-icon name="note_add" />
              {{ 'audits.review.action.raise' | t }}
            </button>
          </div>
        }

        @if (notes().length) {
          <app-review-note-list [notes]="notes()" [itemPrompt]="item().prompt" />
        }
      </section>
    }
  `,
  styles: `
    .item-review {
      display: flex;
      flex-direction: column;
      gap: 10px;
      padding: 12px 14px;
      border: 1px solid var(--mat-sys-outline-variant);
      border-radius: 12px;
      background: var(--mat-sys-surface-container-lowest);
    }
    .item-review__head {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: 6px 12px;
    }
    .item-review__title {
      font-weight: 600;
      font-size: 0.9rem;
    }
    .item-review__state {
      display: inline-flex;
      align-items: center;
      gap: 4px;
      color: var(--mat-sys-on-surface-variant);
      font-size: 0.85rem;
    }
    .item-review__state--done {
      color: #2e7d32;
    }
    :host-context([data-theme='dark']) .item-review__state--done {
      color: #86efac;
    }
    .item-review__hint {
      display: flex;
      align-items: flex-start;
      gap: 6px;
      margin: 0;
      color: var(--mat-sys-on-surface-variant);
      font-size: 0.85rem;
    }
    .item-review__actions {
      display: flex;
      flex-wrap: wrap;
      gap: 8px;
    }
  `,
})
export class ItemReviewComponent {
  readonly store = inject(WorkingPaperReviewStore);
  private readonly auth = inject(AuthService);
  private readonly users = inject(UserLookupService);

  readonly item = input.required<ChecklistProgressItem>();

  /** The item's review; null when there is no review to show (not loaded, or a self-assessment). */
  readonly state = computed(() => {
    const r = this.store.review();
    if (!r || (!r.isRequired && r.notes.length === 0)) {
      return null;
    }
    return this.store.itemReview(this.item().itemId);
  });

  readonly notes = computed(() => {
    // Re-read when the review changes.
    this.store.review();
    return this.store.notesFor(this.item().itemId);
  });

  /** A reviewer, while the audit is open. */
  readonly canAct = computed(() => {
    const r = this.store.review();
    return !!r && r.isOpen && r.canReview;
  });

  readonly answeredByMe = computed(() => {
    const me = this.auth.session()?.userId;
    return !!me && this.state()?.responderUserId === me;
  });

  /** Answered, not by me, not yet signed off, and no note outstanding on it. */
  readonly canSignOff = computed(() => {
    const s = this.state();
    return !!s && s.isAnswered && !s.reviewedBy && s.outstandingNotes === 0 && !this.answeredByMe();
  });

  nameOf(userId: string | null | undefined): string {
    return this.users.displayName(userId);
  }
}
