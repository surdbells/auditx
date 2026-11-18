import { Injectable, inject, signal } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { MatDialog } from '@angular/material/dialog';
import { Observable, Subject } from 'rxjs';

import { AuditsService } from '../../../core/services/audits.service';
import { NotificationService } from '../../../core/services/notification.service';
import { TranslationService } from '../../../core/i18n/translation.service';
import { Audit, ProblemDetails, ReviewItem, ReviewNote, WorkingPaperReview } from '../../../core/models';
import { ReviewNoteDialogComponent, ReviewNoteDialogData } from '../dialogs/review-note-dialog.component';

const CONCURRENCY_CONFLICT = 'audit.concurrency_conflict';

/**
 * The working-paper review of the audit open on the execution page — item sign-offs and review notes — shared by the
 * review card and the open item's review section. Provided by the execution component, so it lives as long as the
 * page. Every change returns the review as it now stands (with the audit's new version); {@link changed} then fires
 * so the page reloads the audit itself.
 */
@Injectable()
export class WorkingPaperReviewStore {
  private readonly service = inject(AuditsService);
  private readonly dialog = inject(MatDialog);
  private readonly notify = inject(NotificationService);
  private readonly i18n = inject(TranslationService);

  /** The review; null until loaded, or when the signed-in user may not see it (the audit's auditee). */
  readonly review = signal<WorkingPaperReview | null>(null);
  /** True while a change is being saved — actions are disabled meanwhile. */
  readonly busy = signal(false);
  /** Fires after a saved change: the audit's version moved, so the page reloads the audit. */
  readonly changed = new Subject<void>();

  private auditId = '';
  /** The newest audit version known — from the audit itself or from the last review change. */
  private version = '';

  /** Called whenever the page's audit (re)loads. */
  setAudit(audit: Audit): void {
    if (audit.id !== this.auditId) {
      this.review.set(null);
    }
    this.auditId = audit.id;
    this.version = audit.version;
  }

  load(): void {
    if (!this.auditId) {
      return;
    }
    const id = this.auditId;
    this.service.getWorkingPaperReview(id).subscribe({
      next: (r) => {
        if (id === this.auditId) {
          this.review.set(r);
        }
      },
      error: () => this.review.set(null),
    });
  }

  clear(): void {
    this.review.set(null);
  }

  itemReview(itemId: string): ReviewItem | null {
    return this.review()?.items.find((i) => i.itemId === itemId) ?? null;
  }

  /** The notes on one item, or (with null) on the audit as a whole — oldest first. */
  notesFor(itemId: string | null): ReviewNote[] {
    return (this.review()?.notes ?? []).filter((n) => n.checklistItemId === itemId);
  }

  /** True when the review is complete: every item signed off and no note outstanding. */
  isComplete(r: WorkingPaperReview | null = this.review()): boolean {
    return !!r && r.signedOffItems === r.totalItems && r.openNotes === 0 && r.answeredNotes === 0;
  }

  signOff(itemId: string): void {
    this.run(this.service.signOffItem(this.auditId, itemId, this.version), 'audits.review.notify.signedOff');
  }

  raise(itemId: string | null, itemPrompt: string | null): void {
    this.ask({ mode: 'raise', itemPrompt }, (body) =>
      this.run(this.service.raiseReviewNote(this.auditId, itemId, body, this.version), 'audits.review.notify.raised'),
    );
  }

  answer(note: ReviewNote, itemPrompt: string | null): void {
    this.ask({ mode: 'answer', itemPrompt, noteBody: note.body }, (answer) =>
      this.run(this.service.answerReviewNote(this.auditId, note.id, answer, this.version), 'audits.review.notify.answered'),
    );
  }

  clearNote(note: ReviewNote): void {
    this.run(this.service.clearReviewNote(this.auditId, note.id, this.version), 'audits.review.notify.cleared');
  }

  reopen(note: ReviewNote): void {
    this.run(this.service.reopenReviewNote(this.auditId, note.id, this.version), 'audits.review.notify.reopened');
  }

  private ask(data: ReviewNoteDialogData, then: (text: string) => void): void {
    this.dialog
      .open(ReviewNoteDialogComponent, { data, width: '560px', maxWidth: '95vw' })
      .afterClosed()
      .subscribe((text?: string) => {
        if (text) {
          then(text);
        }
      });
  }

  private run(request: Observable<WorkingPaperReview>, successKey: string): void {
    if (this.busy()) {
      return;
    }
    this.busy.set(true);
    request.subscribe({
      next: (r) => {
        this.busy.set(false);
        this.review.set(r);
        this.version = r.version;
        this.notify.success(this.i18n.translate(successKey));
        this.changed.next();
      },
      error: (err: unknown) => {
        this.busy.set(false);
        // The interceptor has already explained the refusal. On a stale page, pick up the latest state.
        const problem = err instanceof HttpErrorResponse ? (err.error as ProblemDetails | null) : null;
        if (err instanceof HttpErrorResponse && err.status === 409 && problem?.error_code === CONCURRENCY_CONFLICT) {
          this.changed.next();
          this.load();
        }
      },
    });
  }
}
