import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import {
  MatDialogModule,
  MatDialogRef,
} from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';

import { AppealOutcome } from '../../../../core/models';

export interface DecideAppealDialogResult {
  outcome: AppealOutcome;
  rationale: string;
}

const OUTCOMES: { value: AppealOutcome; label: string }[] = [
  { value: 'confirm', label: 'Confirm' },
  { value: 'modify', label: 'Modify' },
  { value: 'overturn', label: 'Overturn' },
];

/** Captures the appeals authority's decision on a filed appeal. */
@Component({
  selector: 'app-decide-appeal-dialog',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatButtonModule,
  ],
  template: `
    <h2 mat-dialog-title>Decide appeal</h2>
    <mat-dialog-content>
      <form [formGroup]="form" class="form">
        <mat-form-field appearance="outline" class="full">
          <mat-label>Outcome</mat-label>
          <mat-select formControlName="outcome">
            @for (o of outcomes; track o.value) {
              <mat-option [value]="o.value">{{ o.label }}</mat-option>
            }
          </mat-select>
        </mat-form-field>
        <mat-form-field appearance="outline" class="full">
          <mat-label>Rationale</mat-label>
          <textarea matInput formControlName="rationale" rows="3"></textarea>
          @if (
            form.controls.rationale.hasError('required') &&
            form.controls.rationale.touched
          ) {
            <mat-error>A rationale is required.</mat-error>
          }
        </mat-form-field>
      </form>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button matButton type="button" (click)="cancel()">Cancel</button>
      <button
        matButton="filled"
        type="button"
        (click)="submit()"
        [disabled]="form.invalid"
      >
        Save
      </button>
    </mat-dialog-actions>
  `,
  styles: `
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
export class DecideAppealDialogComponent {
  private readonly dialogRef = inject<
    MatDialogRef<DecideAppealDialogComponent, DecideAppealDialogResult>
  >(MatDialogRef);
  private readonly fb = inject(FormBuilder);

  readonly outcomes = OUTCOMES;

  readonly form = this.fb.nonNullable.group({
    outcome: ['confirm' as AppealOutcome, [Validators.required]],
    rationale: ['', [Validators.required]],
  });

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const v = this.form.getRawValue();
    this.dialogRef.close({
      outcome: v.outcome,
      rationale: v.rationale.trim(),
    });
  }

  cancel(): void {
    this.dialogRef.close();
  }
}
