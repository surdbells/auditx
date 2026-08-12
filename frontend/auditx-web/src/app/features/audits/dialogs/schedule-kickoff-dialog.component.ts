import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import {
  MAT_DIALOG_DATA,
  MatDialogModule,
  MatDialogRef,
} from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';

import { ScheduleKickoffRequest } from '../../../core/models';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';

export interface ScheduleKickoffDialogData {
  version: string;
  /** Existing values when rescheduling; empty when scheduling for the first time. */
  scheduledAtUtc: string | null;
  location: string | null;
  agenda: string | null;
}

/** Formats an ISO instant as the local `yyyy-MM-ddThh:mm` a datetime-local input expects. */
function toLocalInput(iso: string | null): string {
  if (!iso) {
    return '';
  }
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) {
    return '';
  }
  const pad = (n: number) => String(n).padStart(2, '0');
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
}

/**
 * Schedule (or reschedule) the pre-audit kickoff meeting. A datetime-local field captures the meeting time in the
 * user's local zone; it is converted to an ISO instant on submit. The auditee is notified automatically by the
 * backend once scheduled.
 */
@Component({
  selector: 'app-schedule-kickoff-dialog',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    TranslatePipe,
  ],
  template: `
    <h2 mat-dialog-title>{{ 'audits.kickoff.dialogTitle' | t }}</h2>
    <mat-dialog-content>
      <p class="hint">{{ 'audits.kickoff.dialogHint' | t }}</p>
      <form [formGroup]="form" class="form">
        <mat-form-field appearance="outline" class="full">
          <mat-label>{{ 'audits.kickoff.field.when' | t }}</mat-label>
          <input matInput type="datetime-local" formControlName="when" />
          @if (form.controls.when.hasError('required') && form.controls.when.touched) {
            <mat-error>{{ 'audits.kickoff.error.when' | t }}</mat-error>
          }
        </mat-form-field>

        <mat-form-field appearance="outline" class="full">
          <mat-label>{{ 'audits.kickoff.field.location' | t }}</mat-label>
          <input matInput formControlName="location" autocomplete="off" />
          <mat-hint>{{ 'audits.kickoff.hint.location' | t }}</mat-hint>
        </mat-form-field>

        <mat-form-field appearance="outline" class="full">
          <mat-label>{{ 'audits.kickoff.field.agenda' | t }}</mat-label>
          <textarea matInput formControlName="agenda" rows="3"></textarea>
        </mat-form-field>
      </form>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button matButton type="button" (click)="cancel()">{{ 'common.cancel' | t }}</button>
      <button matButton="filled" type="button" (click)="submit()" [disabled]="form.invalid">
        {{ 'audits.kickoff.action.schedule' | t }}
      </button>
    </mat-dialog-actions>
  `,
  styles: `
    .hint {
      margin: 0 0 1rem;
      color: var(--mat-sys-on-surface-variant);
      font: var(--mat-sys-body-small);
    }
    .form {
      display: flex;
      flex-direction: column;
      min-width: 420px;
    }
    .full {
      width: 100%;
    }
    @media (max-width: 520px) {
      .form {
        min-width: auto;
      }
    }
  `,
})
export class ScheduleKickoffDialogComponent {
  readonly data = inject<ScheduleKickoffDialogData>(MAT_DIALOG_DATA);
  private readonly dialogRef =
    inject<MatDialogRef<ScheduleKickoffDialogComponent, ScheduleKickoffRequest>>(MatDialogRef);
  private readonly fb = inject(FormBuilder);

  readonly form = this.fb.nonNullable.group({
    when: [toLocalInput(this.data.scheduledAtUtc), [Validators.required]],
    location: [this.data.location ?? ''],
    agenda: [this.data.agenda ?? ''],
  });

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const v = this.form.getRawValue();
    // datetime-local yields a zone-less local string; Date() interprets it as local time, toISOString normalises to UTC.
    const scheduledAtUtc = new Date(v.when).toISOString();
    this.dialogRef.close({
      scheduledAtUtc,
      location: v.location.trim() || null,
      agenda: v.agenda.trim() || null,
      version: this.data.version,
    });
  }

  cancel(): void {
    this.dialogRef.close();
  }
}
