import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { provideNativeDateAdapter } from '@angular/material/core';
import { MatButtonModule } from '@angular/material/button';
import {
  MatDialogModule,
  MatDialogRef,
} from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { SelectSearchDirective } from '../../../shared/directives/select-search.directive';

import { VERIFICATION_RESULTS, VerificationResult } from '../../../core/models';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';
import { MIN_REASON_LENGTH, earliestNewDate, fromDateOnly, toDateOnly } from './date-reason-dialog.component';

export interface VerificationDialogResult {
  result: VerificationResult;
  notes: string | null;
  /** Set for a failed result: the finding reopens, due by this date (yyyy-MM-dd). */
  newTargetDate: string | null;
}

/**
 * Records a post-closure follow-up verification of a finding's remediation (P2-B). A failed result reopens the finding,
 * so it asks what was found and the new date the business must fix it by.
 */
@Component({
  selector: 'app-verification-dialog',
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [provideNativeDateAdapter()],
  imports: [
    MatDatepickerModule,
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
        @if (failed()) {
          <p class="warn">{{ 'exceptions.verification.failedReopens' | t }}</p>
        }
        <mat-form-field appearance="outline" class="full">
          <mat-label>{{ (failed() ? 'exceptions.verification.failedNotes' : 'exceptions.field.verificationNotes') | t }}</mat-label>
          <textarea matInput formControlName="notes" rows="4" maxlength="2000"></textarea>
          @if (failed() && form.controls.notes.touched && notesTooShort()) {
            <mat-error>{{ 'exceptions.extension.reasonError' | t: { min: minNotes } }}</mat-error>
          }
        </mat-form-field>
        @if (failed()) {
          <mat-form-field appearance="outline" class="full">
            <mat-label>{{ 'exceptions.verification.newTargetDate' | t }}</mat-label>
            <input matInput [matDatepicker]="picker" [min]="minDate" formControlName="newTargetDate" />
            <mat-datepicker-toggle matIconSuffix [for]="picker" />
            <mat-datepicker #picker />
          </mat-form-field>
        }
      </form>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button matButton type="button" (click)="cancel()">{{ 'exceptions.action.cancel' | t }}</button>
      <button matButton="filled" type="button" (click)="submit()" [disabled]="!canSave()">
        {{ (failed() ? 'exceptions.verification.saveAndReopen' : 'exceptions.action.save') | t }}
      </button>
    </mat-dialog-actions>
  `,
  styles: `
    .form {
      display: flex;
      flex-direction: column;
      min-width: 0;
    }
    .full {
      width: 100%;
    }
    .warn {
      margin: 0 0 12px;
      color: var(--mat-sys-error);
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
  readonly minNotes = MIN_REASON_LENGTH;
  readonly minDate = fromDateOnly(earliestNewDate(null));

  readonly form = this.fb.group({
    result: this.fb.nonNullable.control<VerificationResult>('passed', [Validators.required]),
    notes: this.fb.nonNullable.control('', [Validators.maxLength(2000)]),
    newTargetDate: this.fb.control<Date | null>(null),
  });

  private readonly value = toSignal(this.form.valueChanges, { initialValue: this.form.getRawValue() });

  readonly failed = (): boolean => this.value().result === 'failed';

  notesTooShort(): boolean {
    return (this.value().notes ?? '').trim().length < MIN_REASON_LENGTH;
  }

  canSave(): boolean {
    if (this.form.invalid) {
      return false;
    }
    if (!this.failed()) {
      return true;
    }
    const date = this.value().newTargetDate;
    return !this.notesTooShort() && !!date && date >= this.minDate;
  }

  submit(): void {
    if (!this.canSave()) {
      this.form.markAllAsTouched();
      return;
    }
    const v = this.form.getRawValue();
    const failed = v.result === 'failed';
    this.dialogRef.close({
      result: v.result,
      notes: v.notes.trim() || null,
      newTargetDate: failed && v.newTargetDate ? toDateOnly(v.newTargetDate) : null,
    });
  }

  cancel(): void {
    this.dialogRef.close();
  }
}
