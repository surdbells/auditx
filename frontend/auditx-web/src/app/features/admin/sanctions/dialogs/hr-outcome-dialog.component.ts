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

import { HrOutcomeType } from '../../../../core/models';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';

export interface HrOutcomeDialogResult {
  outcomeType: HrOutcomeType;
  detail: string;
}

const OUTCOMES: { value: HrOutcomeType; label: string }[] = [
  { value: 'imposed', label: 'sanctions.hrOutcome.imposed' },
  { value: 'declined', label: 'sanctions.hrOutcome.declined' },
  { value: 'modified', label: 'sanctions.hrOutcome.modified' },
  { value: 'dc_referral', label: 'sanctions.referDc.title' },
];

/** Captures the HR authority's outcome on the recommended sanction. */
@Component({
  selector: 'app-hr-outcome-dialog',
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
    <h2 mat-dialog-title>{{ 'sanctions.action.recordHrOutcome' | t }}</h2>
    <mat-dialog-content>
      <form [formGroup]="form" class="form">
        <mat-form-field appearance="outline" class="full">
          <mat-label>{{ 'sanctions.field.outcome' | t }}</mat-label>
          <mat-select formControlName="outcomeType">
            @for (o of outcomes; track o.value) {
              <mat-option [value]="o.value">{{ o.label | t }}</mat-option>
            }
          </mat-select>
        </mat-form-field>
        <mat-form-field appearance="outline" class="full">
          <mat-label>{{ 'sanctions.field.detail' | t }}</mat-label>
          <textarea matInput formControlName="detail" rows="3"></textarea>
          @if (
            form.controls.detail.hasError('required') &&
            form.controls.detail.touched
          ) {
            <mat-error>{{ 'sanctions.dialog.hrOutcome.detailRequired' | t }}</mat-error>
          }
        </mat-form-field>
      </form>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button matButton type="button" (click)="cancel()">{{ 'sanctions.common.cancel' | t }}</button>
      <button
        matButton="filled"
        type="button"
        (click)="submit()"
        [disabled]="form.invalid"
      >
        {{ 'sanctions.common.save' | t }}
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
export class HrOutcomeDialogComponent {
  private readonly dialogRef = inject<
    MatDialogRef<HrOutcomeDialogComponent, HrOutcomeDialogResult>
  >(MatDialogRef);
  private readonly fb = inject(FormBuilder);

  readonly outcomes = OUTCOMES;

  readonly form = this.fb.nonNullable.group({
    outcomeType: ['imposed' as HrOutcomeType, [Validators.required]],
    detail: ['', [Validators.required]],
  });

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const v = this.form.getRawValue();
    this.dialogRef.close({
      outcomeType: v.outcomeType,
      detail: v.detail.trim(),
    });
  }

  cancel(): void {
    this.dialogRef.close();
  }
}
