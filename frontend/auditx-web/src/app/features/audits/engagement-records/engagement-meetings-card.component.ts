import { ChangeDetectionStrategy, Component, computed, inject, input, output } from '@angular/core';
import { DatePipe, NgTemplateOutlet } from '@angular/common';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';

import { IconComponent } from '../../../core/icons/icon.component';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';
import { UserLookupService } from '../../../core/services/user-lookup.service';
import { Audit, AuditMeeting, MeetingKind } from '../../../core/models';
import { EngagementRecordsStore } from './engagement-records.store';

/** Audit states in which each meeting can be recorded (the server's rule). */
const RECORDABLE: Record<MeetingKind, Set<string>> = {
  kickoff: new Set(['planned', 'in_progress', 'under_review', 'completed']),
  exit: new Set(['under_review', 'completed']),
};

/**
 * The engagement's two meetings with management. The kickoff — scheduled before fieldwork (the auditee is told) and,
 * once held, its minutes — and the exit meeting, held on the draft report. Each record lists who attended, the minutes
 * and the points agreed; the auditee reads them, the team records and corrects them.
 */
@Component({
  selector: 'app-engagement-meetings-card',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DatePipe, NgTemplateOutlet, MatCardModule, MatButtonModule, IconComponent, TranslatePipe],
  styleUrl: './engagement-card.scss',
  template: `
    <mat-card appearance="outlined" class="eng" data-guide="meetings">
      <div class="eng__head">
        <h3 class="eng__title">
          <app-icon name="groups" />
          {{ 'engagement.meetings.title' | t }}
        </h3>
      </div>

      <div class="eng__stack">
        <!-- Kickoff: the schedule, then the record of the meeting once held -->
        <section class="eng__section">
          <div class="eng__section-head">
            <h4 class="eng__section-title">{{ 'engagement.meetings.kickoff' | t }}</h4>
            <div class="eng__actions">
              @if (canScheduleKickoff()) {
                <button matButton type="button" (click)="scheduleKickoff.emit()">
                  <app-icon name="event" />
                  {{ (audit().kickoffScheduledAtUtc ? 'audits.kickoff.action.reschedule' : 'audits.kickoff.action.schedule') | t }}
                </button>
              }
              @if (canRecord('kickoff')) {
                <button matButton="tonal" type="button" [disabled]="store.busy()" (click)="record('kickoff')">
                  <app-icon name="edit" />
                  {{ (meeting('kickoff') ? 'engagement.meetings.action.correct' : 'engagement.meetings.action.record') | t }}
                </button>
              }
            </div>
          </div>
          @if (audit().kickoffScheduledAtUtc) {
            <p class="eng__meta">
              <app-icon name="event_note" aria-hidden="true" />
              {{ 'engagement.meetings.scheduledFor' | t: { date: (audit().kickoffScheduledAtUtc | date: 'medium') } }}
              @if (audit().kickoffLocation) { · {{ audit().kickoffLocation }} }
            </p>
            @if (audit().kickoffAgenda && !meeting('kickoff')) {
              <blockquote class="eng__quote">{{ audit().kickoffAgenda }}</blockquote>
            }
          }
          @if (meeting('kickoff'); as m) {
            <ng-container *ngTemplateOutlet="minutes; context: { $implicit: m }" />
          } @else if (!audit().kickoffScheduledAtUtc) {
            <p class="eng__muted">{{ 'engagement.meetings.kickoffNone' | t }}</p>
          } @else {
            <p class="eng__muted">{{ 'engagement.meetings.notRecorded' | t }}</p>
          }
        </section>

        <!-- Exit: held on the draft report -->
        @if (!isSelfAssessment()) {
          <section class="eng__section">
            <div class="eng__section-head">
              <h4 class="eng__section-title">{{ 'engagement.meetings.exit' | t }}</h4>
              @if (canRecord('exit')) {
                <div class="eng__actions">
                  <button matButton="tonal" type="button" [disabled]="store.busy()" (click)="record('exit')">
                    <app-icon name="edit" />
                    {{ (meeting('exit') ? 'engagement.meetings.action.correct' : 'engagement.meetings.action.record') | t }}
                  </button>
                </div>
              }
            </div>
            @if (meeting('exit'); as m) {
              @if (m.reportVersionNumber) {
                <p class="eng__meta">{{ 'engagement.meetings.onDraft' | t: { version: m.reportVersionNumber } }}</p>
              }
              <ng-container *ngTemplateOutlet="minutes; context: { $implicit: m }" />
            } @else if (exitNeedsDraft()) {
              <p class="eng__muted">{{ 'engagement.meetings.exitNeedsDraft' | t }}</p>
            } @else {
              <p class="eng__muted">{{ 'engagement.meetings.exitNone' | t }}</p>
            }
          </section>
        }
      </div>
    </mat-card>

    <ng-template #minutes let-m>
      <p class="eng__meta">
        {{ 'engagement.meetings.heldAt' | t: { date: (m.heldAt | date: 'medium') } }} ·
        {{ 'engagement.meetings.recordedBy' | t: { name: nameOf(m.recordedBy) } }}
        @if (m.lastEditedAt) {
          · {{ 'engagement.meetings.correctedBy' | t: { name: nameOf(m.lastEditedBy), date: (m.lastEditedAt | date: 'medium') } }}
        }
      </p>
      <p class="eng__label">{{ 'engagement.meetings.attendees' | t }}</p>
      <p class="eng__muted">{{ attendees(m) }}</p>
      <p class="eng__label">{{ 'engagement.meetings.minutes' | t }}</p>
      <p class="eng__text">{{ m.minutes }}</p>
      @if (m.agreedPoints.length) {
        <p class="eng__label">{{ 'engagement.meetings.agreedPoints' | t }}</p>
        <ul class="eng__list">
          @for (p of m.agreedPoints; track $index) {
            <li>{{ p }}</li>
          }
        </ul>
      }
    </ng-template>
  `,
})
export class EngagementMeetingsCardComponent {
  readonly store = inject(EngagementRecordsStore);
  private readonly users = inject(UserLookupService);

  readonly audit = input.required<Audit>();
  /** The page decides who may (re)schedule the kickoff (the lifecycle team, before fieldwork). */
  readonly canScheduleKickoff = input(false);
  readonly scheduleKickoff = output<void>();

  readonly isSelfAssessment = computed(() => this.audit().isSelfAssessment);

  /** Under review with no completed draft yet: the exit meeting waits for one. */
  readonly exitNeedsDraft = computed(() => {
    const r = this.store.records();
    return !!r?.canManage && RECORDABLE.exit.has(this.audit().status) && r.draftReports.length === 0;
  });

  meeting(kind: MeetingKind): AuditMeeting | null {
    return this.store.records()?.meetings.find((m) => m.kind === kind) ?? null;
  }

  canRecord(kind: MeetingKind): boolean {
    const r = this.store.records();
    if (!r?.canManage || !RECORDABLE[kind].has(this.audit().status)) {
      return false;
    }
    // The exit meeting names its draft report; a record already made can be corrected regardless.
    return kind === 'kickoff' || r.draftReports.length > 0 || !!this.meeting('exit');
  }

  record(kind: MeetingKind): void {
    const a = this.audit();
    const team = a.teamMembers.filter((m) => m.isActive).map((m) => m.userId);
    this.store.recordMeeting(kind, {
      people: this.users.options().map((u) => ({ id: u.id, name: u.displayName || u.id })),
      defaultAttendeeIds: [...new Set([...team, a.auditeeUserId])],
      scheduledAt: kind === 'kickoff' ? a.kickoffScheduledAtUtc : null,
    });
  }

  nameOf(userId: string | null | undefined): string {
    return this.users.displayName(userId);
  }

  attendees(m: AuditMeeting): string {
    return [...m.attendeeUserIds.map((id) => this.nameOf(id)), ...m.externalAttendees].join(', ');
  }
}
