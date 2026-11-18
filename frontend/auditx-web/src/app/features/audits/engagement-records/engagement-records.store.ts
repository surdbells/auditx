import { Injectable, inject, signal } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { MatDialog } from '@angular/material/dialog';
import { Observable, Subject } from 'rxjs';

import { EngagementRecordsService } from '../../../core/services/engagement-records.service';
import { NotificationService } from '../../../core/services/notification.service';
import { TranslationService } from '../../../core/i18n/translation.service';
import { Audit, EngagementRecords, IndependenceMember, MeetingKind, ProblemDetails, RecordMeetingRequest } from '../../../core/models';
import { ConfirmDialogComponent, ConfirmDialogData } from '../../../shared/components/confirm-dialog/confirm-dialog.component';
import { EngagementTextDialogComponent, EngagementTextDialogData } from './dialogs/engagement-text-dialog.component';
import {
  DeclareIndependenceDialogComponent,
  DeclareIndependenceResult,
  INDEPENDENCE_TEXT_MAX_LENGTH,
} from './dialogs/declare-independence-dialog.component';
import { RecordMeetingDialogComponent, RecordMeetingDialogData } from './dialogs/record-meeting-dialog.component';

/** The server's limits (EngagementLetter.MaxBodyLength / MaxCommentLength). */
export const LETTER_MAX_LENGTH = 20000;
export const LETTER_COMMENT_MAX_LENGTH = 2000;

/** Refusals that mean the page is out of date: pick up the latest records. */
const STALE = new Set(['audit.concurrency_conflict', 'engagement_letter.concurrency_conflict', 'meeting.concurrency_conflict']);

/**
 * The engagement records of the audit open on the audit page — its engagement letter, its team's independence and its
 * kickoff and exit meeting records — shared by the three cards that show them. Provided by the audit page. Every change
 * answers with the records as they now stand; when it moved the audit's own version (declaring or accepting a
 * conflict), {@link auditChanged} fires so the page reloads the audit.
 */
@Injectable()
export class EngagementRecordsStore {
  private readonly service = inject(EngagementRecordsService);
  private readonly dialog = inject(MatDialog);
  private readonly notify = inject(NotificationService);
  private readonly i18n = inject(TranslationService);

  readonly records = signal<EngagementRecords | null>(null);
  readonly busy = signal(false);
  /** Fires after a change that moved the audit's version: the page reloads the audit. */
  readonly auditChanged = new Subject<void>();

  private auditId = '';
  private auditVersion = '';

  /** Called whenever the page's audit (re)loads. */
  setAudit(audit: Audit): void {
    if (audit.id !== this.auditId) {
      this.records.set(null);
    }
    this.auditId = audit.id;
    this.auditVersion = audit.version;
  }

  load(): void {
    if (!this.auditId) {
      return;
    }
    const id = this.auditId;
    this.service.get(id).subscribe({
      next: (r) => {
        if (id === this.auditId) {
          this.records.set(r);
        }
      },
      error: () => this.records.set(null),
    });
  }

  /* ---- Engagement letter ---- */

  /** Write the letter (from a starting text the card prepares) or change it. */
  writeLetter(startingText: string): void {
    const letter = this.records()?.letter ?? null;
    const data: EngagementTextDialogData = {
      title: letter ? 'engagement.letter.dialog.editTitle' : 'engagement.letter.dialog.writeTitle',
      hint: letter && letter.status !== 'draft' ? 'engagement.letter.dialog.reviseHint' : 'engagement.letter.dialog.hint',
      label: 'engagement.letter.dialog.label',
      submit: 'engagement.letter.dialog.save',
      value: letter?.body ?? startingText,
      required: true,
      maxLength: LETTER_MAX_LENGTH,
      rows: 16,
    };
    this.askText(data, '760px', (body) =>
      this.run(this.service.saveLetter(this.auditId, body, letter?.version ?? null), 'engagement.letter.notify.saved', false),
    );
  }

  issueLetter(auditeeName: string): void {
    const letter = this.records()?.letter;
    if (!letter) {
      return;
    }
    const data: ConfirmDialogData = {
      title: this.i18n.translate('engagement.letter.confirmIssue.title'),
      message: this.i18n.translate('engagement.letter.confirmIssue.message', { name: auditeeName }),
      confirmLabel: this.i18n.translate('engagement.letter.action.issue'),
      cancelLabel: this.i18n.translate('common.cancel'),
    };
    this.dialog
      .open(ConfirmDialogComponent, { data })
      .afterClosed()
      .subscribe((ok) => {
        if (ok) {
          this.run(this.service.issueLetter(this.auditId, letter.version), 'engagement.letter.notify.issued', false);
        }
      });
  }

  acknowledgeLetter(): void {
    const letter = this.records()?.letter;
    if (!letter) {
      return;
    }
    const data: EngagementTextDialogData = {
      title: 'engagement.letter.dialog.acknowledgeTitle',
      hint: 'engagement.letter.dialog.acknowledgeHint',
      label: 'engagement.letter.dialog.commentLabel',
      submit: 'engagement.letter.action.acknowledge',
      required: false,
      maxLength: LETTER_COMMENT_MAX_LENGTH,
      rows: 3,
    };
    this.askText(data, '560px', (comment) =>
      this.run(this.service.acknowledgeLetter(letter.id, comment || null, letter.version), 'engagement.letter.notify.acknowledged', false),
    );
  }

  /* ---- Independence ---- */

  declareIndependence(): void {
    this.dialog
      .open(DeclareIndependenceDialogComponent, { width: '560px', maxWidth: '95vw' })
      .afterClosed()
      .subscribe((result?: DeclareIndependenceResult) => {
        if (result) {
          this.run(
            this.service.declareIndependence(this.auditId, result.hasConflict, result.conflictDetails, this.currentAuditVersion()),
            result.hasConflict ? 'engagement.independence.notify.disclosed' : 'engagement.independence.notify.declared',
            true,
          );
        }
      });
  }

  acceptConflict(member: IndependenceMember, memberName: string): void {
    if (!member.declarationId) {
      return;
    }
    const declarationId = member.declarationId;
    const data: EngagementTextDialogData = {
      title: 'engagement.independence.accept.title',
      hint: 'engagement.independence.accept.hint',
      label: 'engagement.independence.accept.label',
      submit: 'engagement.independence.action.accept',
      subject: this.i18n.translate('engagement.independence.accept.subject', { name: memberName }),
      quote: member.conflictDetails ?? undefined,
      required: true,
      maxLength: INDEPENDENCE_TEXT_MAX_LENGTH,
      rows: 4,
    };
    this.askText(data, '560px', (safeguards) =>
      this.run(
        this.service.acceptConflict(this.auditId, declarationId, safeguards, this.currentAuditVersion()),
        'engagement.independence.notify.accepted',
        true,
      ),
    );
  }

  /* ---- Meetings ---- */

  recordMeeting(kind: MeetingKind, context: Omit<RecordMeetingDialogData, 'kind' | 'meeting' | 'draftReports'>): void {
    const records = this.records();
    if (!records) {
      return;
    }
    const meeting = records.meetings.find((m) => m.kind === kind) ?? null;
    const data: RecordMeetingDialogData = { ...context, kind, meeting, draftReports: records.draftReports };
    this.dialog
      .open(RecordMeetingDialogComponent, { data, width: '640px', maxWidth: '95vw' })
      .afterClosed()
      .subscribe((request?: Omit<RecordMeetingRequest, 'meetingVersion'>) => {
        if (request) {
          this.run(
            this.service.recordMeeting(this.auditId, kind, { ...request, meetingVersion: meeting?.version ?? null }),
            meeting ? 'engagement.meetings.notify.corrected' : 'engagement.meetings.notify.recorded',
            false,
          );
        }
      });
  }

  /** The newest audit version known: from the last records answer, or the audit itself. */
  private currentAuditVersion(): string {
    return this.records()?.auditVersion || this.auditVersion;
  }

  private askText(data: EngagementTextDialogData, width: string, then: (text: string) => void): void {
    this.dialog
      .open(EngagementTextDialogComponent, { data, width, maxWidth: '95vw' })
      .afterClosed()
      .subscribe((text?: string) => {
        if (text !== undefined && text !== null) {
          then(text);
        }
      });
  }

  private run(request: Observable<EngagementRecords>, successKey: string, movesAuditVersion: boolean): void {
    if (this.busy()) {
      return;
    }
    this.busy.set(true);
    request.subscribe({
      next: (r) => {
        this.busy.set(false);
        this.records.set(r);
        this.auditVersion = r.auditVersion;
        this.notify.success(this.i18n.translate(successKey));
        if (movesAuditVersion) {
          this.auditChanged.next();
        }
      },
      error: (err: unknown) => {
        this.busy.set(false);
        // The interceptor has already explained the refusal. On a stale page, pick up the latest state.
        const problem = err instanceof HttpErrorResponse ? (err.error as ProblemDetails | null) : null;
        if (err instanceof HttpErrorResponse && err.status === 409 && problem?.error_code && STALE.has(problem.error_code)) {
          this.auditChanged.next();
          this.load();
        }
      },
    });
  }
}
