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

import { VERIFICATION_RESULTS, VerificationResult } from '../../../core/models';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';

export interface VerificationDialogResult {
  result: VerificationResult;
  notes: string | null;
}

/** Records a post-closure follow-up verification of a finding's remediation (P2-B). */
@Component({
  selector: 'app-verification-dialog',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatButtonModule,
    TranslatePipe,
  ],
  template: `
    <h2 mat-dialog-title>{{ 'exceptions.dialog.verificationTitle' | t }}</h2>
    <mat-dialog-content>
      <form [formGroup]="form" class="form">
        <mat-form-field appearance="outline" class="full">
          <mat-label>{{ 'exceptions.field.verificationResult' | t }}</mat-label>
          <mat-select formControlName="result">
            @for (r of results; track r) {
              <mat-option [value]="r">{{ 'exceptions.verificationResult.' + r | t }}</mat-option>
            }
          </mat-select>
        </mat-form-field>
        <mat-form-field appearance="outline" class="full">
          <mat-label>{{ 'exceptions.field.verificationNotes' | t }}</mat-label>
          <textarea matInput formControlName="notes" rows="4"></textarea>
        </mat-form-field>
      </form>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button matButton type="button" (click)="cancel()">{{ 'exceptions.action.cancel' | t }}</button>
      <button matButton="filled" type="button" (click)="submit()" [disabled]="form.invalid">
        {{ 'exceptions.action.save' | t }}
      </button>
    </mat-dialog-actions>
  `,
  styles: `
    .form {
      display: flex;
      flex-direction: column;
      min-width: 440px;
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
export class VerificationDialogComponent {
  private readonly dialogRef = inject<MatDialogRef<VerificationDialogComponent, VerificationDialogResult>>(MatDialogRef);
  private readonly fb = inject(FormBuilder);

  readonly results = VERIFICATION_RESULTS;

  readonly form = this.fb.nonNullable.group({
    result: ['passed' as VerificationResult, [Validators.required]],
    notes: ['', [Validators.maxLength(2000)]],
  });

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const v = this.form.getRawValue();
    this.dialogRef.close({ result: v.result, notes: v.notes.trim() || null });
  }

  cancel(): void {
    this.dialogRef.close();
  }
}
