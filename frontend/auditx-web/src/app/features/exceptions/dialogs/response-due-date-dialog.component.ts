import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import {
  MAT_DIALOG_DATA,
  MatDialogModule,
  MatDialogRef,
} from '@angular/material/dialog';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { provideNativeDateAdapter } from '@angular/material/core';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';

import { TranslatePipe } from '../../../core/i18n/translate.pipe';

export interface ResponseDueDateDialogData {
  /** Current due date as an ISO date-only string (yyyy-MM-dd), or null. */
  dueDate: string | null;
}

/** Result: the chosen due date as a date-only string (yyyy-MM-dd), or null to clear it. */
export interface ResponseDueDateDialogResult {
  dueDate: string | null;
}

/** Format a local Date as a date-only (yyyy-MM-dd) string — avoids the UTC day-shift toISOString() would cause. */
function toDateOnly(d: Date | null): string | null {
  if (!d) {
    return null;
  }
  const y = d.getFullYear();
  const m = String(d.getMonth() + 1).padStart(2, '0');
  const day = String(d.getDate()).padStart(2, '0');
  return `${y}-${m}-${day}`;
}

/** Sets (or clears) the management-response due date for a finding. */
@Component({
  selector: 'app-response-due-date-dialog',
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
    <h2 mat-dialog-title>{{ 'exceptions.responseDue.title' | t }}</h2>
    <mat-dialog-content>
      <p class="hint">{{ 'exceptions.responseDue.hint' | t }}</p>
      <form [formGroup]="form" class="form">
        <mat-form-field appearance="outline" class="full">
          <mat-label>{{ 'exceptions.responseDue.label' | t }}</mat-label>
          <input matInput [matDatepicker]="picker" formControlName="dueDate" />
          <mat-datepicker-toggle matIconSuffix [for]="picker" />
          <mat-datepicker #picker />
          <mat-hint>{{ 'exceptions.responseDue.clearHint' | t }}</mat-hint>
        </mat-form-field>
      </form>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button matButton type="button" (click)="cancel()">{{ 'exceptions.action.cancel' | t }}</button>
      <button matButton="filled" type="button" (click)="submit()">
        {{ 'exceptions.responseDue.save' | t }}
      </button>
    </mat-dialog-actions>
  `,
  styles: `
    .form {
      display: flex;
      flex-direction: column;
      min-width: 320px;
    }
    .full {
      width: 100%;
    }
    .hint {
      color: var(--mat-sys-on-surface-variant);
      margin-top: 0;
    }
    @media (max-width: 480px) {
      .form {
        min-width: auto;
      }
    }
  `,
})
export class ResponseDueDateDialogComponent {
  readonly data = inject<ResponseDueDateDialogData>(MAT_DIALOG_DATA);
  private readonly dialogRef =
    inject<MatDialogRef<ResponseDueDateDialogComponent, ResponseDueDateDialogResult>>(MatDialogRef);
  private readonly fb = inject(FormBuilder);

  readonly form = this.fb.group({
    // Parse the incoming date-only string at local midnight so the picker shows the exact day.
    dueDate: [this.data.dueDate ? new Date(this.data.dueDate + 'T00:00:00') : null],
  });

  submit(): void {
    this.dialogRef.close({ dueDate: toDateOnly(this.form.controls.dueDate.value) });
  }

  cancel(): void {
    this.dialogRef.close();
  }
}
