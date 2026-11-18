import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { DatePipe } from '@angular/common';
import { AbstractControl, FormBuilder, ReactiveFormsModule, ValidationErrors, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';

import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { SelectSearchDirective } from '../../../../shared/directives/select-search.directive';
import { AuditMeeting, DraftReportOption, MeetingKind, RecordMeetingRequest } from '../../../../core/models';

/** The server's limits for a meeting record. */
export const MEETING_LIMITS = { minutes: 8000, point: 1000, points: 50, attendees: 100, externalAttendee: 200 } as const;

export interface RecordMeetingDialogData {
  kind: MeetingKind;
  /** The record being corrected; null for a new one. */
  meeting: AuditMeeting | null;
  /** Everyone who can be named as an attendee from the directory. */
  people: { id: string; name: string }[];
  /** Who is ticked by default for a new record: the audit's team and its auditee. */
  defaultAttendeeIds: string[];
  /** For the exit meeting: the completed draft reports it can be held on, newest first. */
  draftReports: DraftReportOption[];
  /** For the kickoff: when it was scheduled, to start the date from. */
  scheduledAt: string | null;
}

/** "YYYY-MM-DDTHH:mm" in the browser's time zone, for a datetime-local input. */
function toLocalInput(value: Date): string {
  const pad = (n: number) => String(n).padStart(2, '0');
  return `${value.getFullYear()}-${pad(value.getMonth() + 1)}-${pad(value.getDate())}T${pad(value.getHours())}:${pad(value.getMinutes())}`;
}

/** One entry per line, trimmed, blanks dropped. */
function lines(value: string): string[] {
  return value
    .split(/\r?\n/)
    .map((l) => l.trim())
    .filter((l) => l.length > 0);
}

/**
 * Records (or corrects) the minutes of the kickoff or exit meeting: when it was held, who attended (from the directory,
 * and others by name), the minutes and the points agreed; the exit meeting also names the draft report it discussed.
 * Closes with the request (without the record's version), or nothing when cancelled.
 */
@Component({
  selector: 'app-record-meeting-dialog',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    DatePipe,
    ReactiveFormsModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    SelectSearchDirective,
    MatButtonModule,
    TranslatePipe,
  ],
  template: `
    <h2 mat-dialog-title>{{ 'engagement.meetings.dialog.title.' + data.kind | t }}</h2>
    <mat-dialog-content>
      <p class="hint">{{ 'engagement.meetings.dialog.hint.' + data.kind | t }}</p>
      <form [formGroup]="form" class="form">
        <mat-form-field appearance="outline">
          <mat-label>{{ 'engagement.meetings.dialog.heldAt' | t }}</mat-label>
          <input matInput type="datetime-local" formControlName="heldAt" [max]="maxHeldAt" />
          @if (form.controls.heldAt.hasError('required') && form.controls.heldAt.touched) {
            <mat-error>{{ 'engagement.meetings.dialog.heldAtRequired' | t }}</mat-error>
          } @else if (form.controls.heldAt.hasError('future')) {
            <mat-error>{{ 'engagement.meetings.dialog.heldAtFuture' | t }}</mat-error>
          }
        </mat-form-field>

        @if (data.kind === 'exit') {
          <mat-form-field appearance="outline">
            <mat-label>{{ 'engagement.meetings.dialog.report' | t }}</mat-label>
            <mat-select formControlName="reportId">
              @for (r of data.draftReports; track r.id) {
                <mat-option [value]="r.id">
                  {{ 'engagement.meetings.draftVersion' | t: { version: r.versionNumber } }}
                  @if (r.completedAt) { · {{ r.completedAt | date: 'mediumDate' }} }
                </mat-option>
              }
            </mat-select>
            @if (form.controls.reportId.hasError('required') && form.controls.reportId.touched) {
              <mat-error>{{ 'engagement.meetings.dialog.reportRequired' | t }}</mat-error>
            }
          </mat-form-field>
        }

        <mat-form-field appearance="outline">
          <mat-label>{{ 'engagement.meetings.dialog.attendees' | t }}</mat-label>
          <mat-select formControlName="attendeeUserIds" multiple>
            @for (p of data.people; track p.id) {
              <mat-option [value]="p.id">{{ p.name }}</mat-option>
            }
          </mat-select>
        </mat-form-field>

        <mat-form-field appearance="outline">
          <mat-label>{{ 'engagement.meetings.dialog.externalAttendees' | t }}</mat-label>
          <textarea matInput formControlName="externalAttendees" rows="2"></textarea>
          <mat-hint>{{ 'engagement.meetings.dialog.onePerLine' | t }}</mat-hint>
          @if (form.controls.externalAttendees.hasError('lineTooLong')) {
            <mat-error>{{ 'engagement.meetings.dialog.attendeeTooLong' | t: { max: limits.externalAttendee } }}</mat-error>
          }
        </mat-form-field>
        @if (form.hasError('noAttendees') && form.touched) {
          <p class="error">{{ 'engagement.meetings.dialog.attendeesRequired' | t }}</p>
        }

        <mat-form-field appearance="outline">
          <mat-label>{{ 'engagement.meetings.dialog.minutes' | t }}</mat-label>
          <textarea matInput formControlName="minutes" rows="6"></textarea>
          <mat-hint align="end">{{ form.controls.minutes.value.length }} / {{ limits.minutes }}</mat-hint>
          @if ((form.controls.minutes.hasError('required') || form.controls.minutes.hasError('pattern')) && form.controls.minutes.touched) {
            <mat-error>{{ 'engagement.meetings.dialog.minutesRequired' | t }}</mat-error>
          } @else if (form.controls.minutes.hasError('maxlength')) {
            <mat-error>{{ 'engagement.dialog.tooLong' | t: { max: limits.minutes } }}</mat-error>
          }
        </mat-form-field>

        <mat-form-field appearance="outline">
          <mat-label>{{ 'engagement.meetings.dialog.agreedPoints' | t }}</mat-label>
          <textarea matInput formControlName="agreedPoints" rows="4"></textarea>
          <mat-hint>{{ 'engagement.meetings.dialog.onePerLine' | t }}</mat-hint>
          @if (form.controls.agreedPoints.hasError('lineTooLong')) {
            <mat-error>{{ 'engagement.meetings.dialog.pointTooLong' | t: { max: limits.point } }}</mat-error>
          } @else if (form.controls.agreedPoints.hasError('tooManyLines')) {
            <mat-error>{{ 'engagement.meetings.dialog.tooManyPoints' | t: { max: limits.points } }}</mat-error>
          }
        </mat-form-field>
      </form>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button matButton type="button" mat-dialog-close>{{ 'common.cancel' | t }}</button>
      <button matButton="filled" type="button" (click)="submit()" [disabled]="form.invalid">
        {{ (data.meeting ? 'engagement.meetings.dialog.saveCorrection' : 'engagement.meetings.dialog.save') | t }}
      </button>
    </mat-dialog-actions>
  `,
  styles: `
    .hint {
      margin: 0 0 12px;
      color: var(--mat-sys-on-surface-variant);
      font-size: 0.875rem;
      line-height: 1.45;
    }
    .form {
      display: flex;
      flex-direction: column;
      gap: 4px;
    }
    .error {
      margin: -4px 0 8px;
      color: var(--mat-sys-error);
      font-size: 0.78rem;
    }
  `,
})
export class RecordMeetingDialogComponent {
  readonly data = inject<RecordMeetingDialogData>(MAT_DIALOG_DATA);
  private readonly dialogRef = inject<MatDialogRef<RecordMeetingDialogComponent, Omit<RecordMeetingRequest, 'meetingVersion'>>>(MatDialogRef);
  private readonly fb = inject(FormBuilder);

  readonly limits = MEETING_LIMITS;
  /** A meeting is recorded once it has been held. */
  readonly maxHeldAt = toLocalInput(new Date());

  readonly form = this.fb.nonNullable.group(
    {
      heldAt: [this.initialHeldAt(), [Validators.required, notInFuture]],
      reportId: [this.data.meeting?.reportId ?? this.data.draftReports[0]?.id ?? '', this.data.kind === 'exit' ? [Validators.required] : []],
      attendeeUserIds: [this.data.meeting?.attendeeUserIds ?? this.data.defaultAttendeeIds],
      externalAttendees: [(this.data.meeting?.externalAttendees ?? []).join('\n'), [linesNoLongerThan(MEETING_LIMITS.externalAttendee)]],
      minutes: [this.data.meeting?.minutes ?? '', [Validators.required, Validators.pattern(/\S/), Validators.maxLength(MEETING_LIMITS.minutes)]],
      agreedPoints: [(this.data.meeting?.agreedPoints ?? []).join('\n'), [linesNoLongerThan(MEETING_LIMITS.point), atMostLines(MEETING_LIMITS.points)]],
    },
    { validators: [someoneAttended] },
  );

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const v = this.form.getRawValue();
    this.dialogRef.close({
      heldAt: new Date(v.heldAt).toISOString(),
      attendeeUserIds: v.attendeeUserIds,
      externalAttendees: lines(v.externalAttendees),
      minutes: v.minutes.trim(),
      agreedPoints: lines(v.agreedPoints),
      reportId: this.data.kind === 'exit' ? v.reportId || null : null,
    });
  }

  private initialHeldAt(): string {
    if (this.data.meeting) {
      return toLocalInput(new Date(this.data.meeting.heldAt));
    }
    const scheduled = this.data.scheduledAt ? new Date(this.data.scheduledAt) : null;
    return toLocalInput(scheduled && scheduled.getTime() <= Date.now() ? scheduled : new Date());
  }
}

function notInFuture(control: AbstractControl): ValidationErrors | null {
  const value = control.value as string;
  // A few minutes' allowance, as the server gives, for clocks that differ.
  return value && new Date(value).getTime() > Date.now() + 5 * 60_000 ? { future: true } : null;
}

function linesNoLongerThan(max: number) {
  return (control: AbstractControl): ValidationErrors | null =>
    lines((control.value as string) ?? '').some((l) => l.length > max) ? { lineTooLong: true } : null;
}

function atMostLines(max: number) {
  return (control: AbstractControl): ValidationErrors | null =>
    lines((control.value as string) ?? '').length > max ? { tooManyLines: true } : null;
}

function someoneAttended(group: AbstractControl): ValidationErrors | null {
  const ids = (group.get('attendeeUserIds')?.value as string[]) ?? [];
  const others = lines((group.get('externalAttendees')?.value as string) ?? '');
  return ids.length + others.length === 0 ? { noAttendees: true } : null;
}
