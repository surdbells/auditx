import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { provideNativeDateAdapter } from '@angular/material/core';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';

import { TranslatePipe } from '../../../core/i18n/translate.pipe';

/** Shortest reason the server accepts for asking for more time or reopening. */
export const MIN_REASON_LENGTH = 20;

export interface DateReasonDialogData {
  title: string;
  message?: string;
  dateLabel: string;
  reasonLabel: string;
  confirmLabel: string;
  /** Earliest date that can be picked, as yyyy-MM-dd. */
  minDate: string;
  /** Shown above the form, e.g. the date that applies today. */
  currentDateNote?: string;
  destructive?: boolean;
}

export interface DateReasonDialogResult {
  /** The chosen date as yyyy-MM-dd. */
  date: string;
  reason: string;
}

/** Format a local Date as yyyy-MM-dd (avoids the UTC day-shift toISOString() would cause). */
export function toDateOnly(d: Date): string {
  const y = d.getFullYear();
  const m = String(d.getMonth() + 1).padStart(2, '0');
  const day = String(d.getDate()).padStart(2, '0');
  return `${y}-${m}-${day}`;
}

/** Parse yyyy-MM-dd as a local date. */
export function fromDateOnly(s: string): Date {
  const [y, m, d] = s.split('-').map(Number);
  return new Date(y, m - 1, d);
}

/** The later of tomorrow and the day after `date` (yyyy-MM-dd): the earliest acceptable new due date. */
export function earliestNewDate(date: string | null | undefined): string {
  const tomorrow = new Date();
  tomorrow.setHours(0, 0, 0, 0);
  tomorrow.setDate(tomorrow.getDate() + 1);
  if (!date) {
    return toDateOnly(tomorrow);
  }
  const after = fromDateOnly(date);
  after.setDate(after.getDate() + 1);
  return toDateOnly(after > tomorrow ? after : tomorrow);
}

/** Captures a new date and the reason for it: asking for more time, or reopening a finding. */
@Component({
  selector: 'app-date-reason-dialog',
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [provideNativeDateAdapter()],
  imports: [
    ReactiveFormsModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatDatepickerModule,
    MatButtonModule,
    TranslatePipe,
  ],
  template: `
    <h2 mat-dialog-title>{{ data.title }}</h2>
    <mat-dialog-content>
      @if (data.message) {
        <p class="message">{{ data.message }}</p>
      }
      @if (data.currentDateNote) {
        <p class="current">{{ data.currentDateNote }}</p>
      }
      <form [formGroup]="form" class="form">
        <mat-form-field appearance="outline" class="full">
          <mat-label>{{ data.dateLabel }}</mat-label>
          <input matInput [matDatepicker]="picker" [min]="min" formControlName="date" required />
          <mat-datepicker-toggle matIconSuffix [for]="picker" />
          <mat-datepicker #picker />
          @if (form.controls.date.touched && form.controls.date.invalid) {
            <mat-error>{{ 'exceptions.extension.dateError' | t }}</mat-error>
          }
        </mat-form-field>
        <mat-form-field appearance="outline" class="full">
          <mat-label>{{ data.reasonLabel }}</mat-label>
          <textarea matInput formControlName="reason" rows="4" maxlength="2000"></textarea>
          <mat-hint>{{ 'exceptions.extension.reasonHint' | t: { min: minReason } }}</mat-hint>
          @if (form.controls.reason.touched && form.controls.reason.invalid) {
            <mat-error>{{ 'exceptions.extension.reasonError' | t: { min: minReason } }}</mat-error>
          }
        </mat-form-field>
      </form>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button matButton type="button" (click)="cancel()">{{ 'exceptions.action.cancel' | t }}</button>
      <button matButton="filled" type="button" [class.destructive]="data.destructive" [disabled]="form.invalid" (click)="submit()">
        {{ data.confirmLabel }}
      </button>
    </mat-dialog-actions>
  `,
  styles: `
    .form {
      display: flex;
      flex-direction: column;
      gap: 4px;
      min-width: 420px;
    }
    .full {
      width: 100%;
    }
    .message,
    .current {
      margin: 0 0 12px;
    }
    .current {
      color: var(--mat-sys-on-surface-variant);
    }
    .destructive {
      --mat-filled-button-container-color: var(--mat-sys-error);
      --mat-filled-button-label-text-color: var(--mat-sys-on-error);
    }
    @media (max-width: 520px) {
      .form {
        min-width: auto;
      }
    }
  `,
})
export class DateReasonDialogComponent {
  readonly data = inject<DateReasonDialogData>(MAT_DIALOG_DATA);
  private readonly dialogRef = inject<MatDialogRef<DateReasonDialogComponent, DateReasonDialogResult>>(MatDialogRef);
  private readonly fb = inject(FormBuilder);

  readonly minReason = MIN_REASON_LENGTH;
  readonly min = fromDateOnly(this.data.minDate);

  readonly form = this.fb.group({
    date: [null as Date | null, [Validators.required]],
    reason: ['', [Validators.required, Validators.minLength(MIN_REASON_LENGTH), Validators.maxLength(2000)]],
  });

  submit(): void {
    const { date, reason } = this.form.getRawValue();
    const trimmed = (reason ?? '').trim();
    if (this.form.invalid || !date || date < this.min || trimmed.length < MIN_REASON_LENGTH) {
      this.form.markAllAsTouched();
      return;
    }
    this.dialogRef.close({ date: toDateOnly(date), reason: trimmed });
  }

  cancel(): void {
    this.dialogRef.close();
  }
}
