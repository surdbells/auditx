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

import { DcDecisionType } from '../../../../core/models';

export interface DcDecisionDialogResult {
  decision: DcDecisionType;
  rationale: string;
  votingRecord: string | null;
}

const DECISIONS: { value: DcDecisionType; label: string }[] = [
  { value: 'uphold', label: 'Uphold' },
  { value: 'modify', label: 'Modify' },
  { value: 'dismiss', label: 'Dismiss' },
];

/** Captures the disciplinary committee's decision on a referred case. */
@Component({
  selector: 'app-dc-decision-dialog',
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
    <h2 mat-dialog-title>Record DC decision</h2>
    <mat-dialog-content>
      <form [formGroup]="form" class="form">
        <mat-form-field appearance="outline" class="full">
          <mat-label>Decision</mat-label>
          <mat-select formControlName="decision">
            @for (d of decisions; track d.value) {
              <mat-option [value]="d.value">{{ d.label }}</mat-option>
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
        <mat-form-field appearance="outline" class="full">
          <mat-label>Voting record (optional)</mat-label>
          <textarea matInput formControlName="votingRecord" rows="2"></textarea>
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
export class DcDecisionDialogComponent {
  private readonly dialogRef = inject<
    MatDialogRef<DcDecisionDialogComponent, DcDecisionDialogResult>
  >(MatDialogRef);
  private readonly fb = inject(FormBuilder);

  readonly decisions = DECISIONS;

  readonly form = this.fb.nonNullable.group({
    decision: ['uphold' as DcDecisionType, [Validators.required]],
    rationale: ['', [Validators.required]],
    votingRecord: [''],
  });

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const v = this.form.getRawValue();
    this.dialogRef.close({
      decision: v.decision,
      rationale: v.rationale.trim(),
      votingRecord: v.votingRecord.trim() || null,
    });
  }

  cancel(): void {
    this.dialogRef.close();
  }
}
