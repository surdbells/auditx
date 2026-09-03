import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import {
  MAT_DIALOG_DATA,
  MatDialogModule,
  MatDialogRef,
} from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatSelectModule } from '@angular/material/select';

import { TranslatePipe } from '../../../core/i18n/translate.pipe';

/** A prior finding this one can be marked as a recurrence of. */
export interface RecurrenceCandidate {
  id: string;
  title: string;
}

export interface MarkRecurrenceDialogData {
  candidates: RecurrenceCandidate[];
  currentPriorId?: string | null;
}

export interface MarkRecurrenceDialogResult {
  recurrenceOfExceptionId: string;
}

/** Marks a finding as a recurrence of a prior finding — pick the earlier finding it repeats. */
@Component({
  selector: 'app-mark-recurrence-dialog',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    MatDialogModule,
    MatFormFieldModule,
    MatSelectModule,
    MatButtonModule,
    TranslatePipe,
  ],
  template: `
    <h2 mat-dialog-title>{{ 'exceptions.recurrence.dialogTitle' | t }}</h2>
    <mat-dialog-content>
      <p class="hint">{{ 'exceptions.recurrence.dialogHint' | t }}</p>
      @if (!data.candidates.length) {
        <p class="muted">{{ 'exceptions.recurrence.noCandidates' | t }}</p>
      } @else {
        <form [formGroup]="form" class="form">
          <mat-form-field appearance="outline" class="full">
            <mat-label>{{ 'exceptions.recurrence.priorFinding' | t }}</mat-label>
            <mat-select formControlName="recurrenceOfExceptionId">
              @for (c of data.candidates; track c.id) {
                <mat-option [value]="c.id">{{ c.title }}</mat-option>
              }
            </mat-select>
            @if (form.controls.recurrenceOfExceptionId.hasError('required') && form.controls.recurrenceOfExceptionId.touched) {
              <mat-error>{{ 'exceptions.recurrence.selectPrior' | t }}</mat-error>
            }
          </mat-form-field>
        </form>
      }
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button matButton type="button" (click)="cancel()">{{ 'exceptions.action.cancel' | t }}</button>
      <button matButton="filled" type="button" (click)="submit()" [disabled]="form.invalid || !data.candidates.length">
        {{ 'exceptions.recurrence.mark' | t }}
      </button>
    </mat-dialog-actions>
  `,
  styles: `
    .hint { margin: 0 0 0.75rem; color: var(--mat-sys-on-surface-variant); }
    .muted { color: var(--mat-sys-on-surface-variant); }
    .form { display: flex; flex-direction: column; min-width: 420px; }
    .full { width: 100%; }
    @media (max-width: 520px) { .form { min-width: auto; } }
  `,
})
export class MarkRecurrenceDialogComponent {
  readonly data = inject<MarkRecurrenceDialogData>(MAT_DIALOG_DATA);
  private readonly dialogRef =
    inject<MatDialogRef<MarkRecurrenceDialogComponent, MarkRecurrenceDialogResult>>(MatDialogRef);
  private readonly fb = inject(FormBuilder);

  readonly form = this.fb.nonNullable.group({
    recurrenceOfExceptionId: [this.data.currentPriorId ?? '', [Validators.required]],
  });

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    this.dialogRef.close({ recurrenceOfExceptionId: this.form.getRawValue().recurrenceOfExceptionId });
  }

  cancel(): void {
    this.dialogRef.close();
  }
}
