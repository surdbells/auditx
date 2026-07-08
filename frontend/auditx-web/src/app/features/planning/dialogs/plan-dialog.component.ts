import {
  ChangeDetectionStrategy,
  Component,
  inject,
} from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
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

import { Plan, SavePlanRequest } from '../../../core/models';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';

export interface PlanDialogData {
  /** Present when editing an existing plan. */
  plan?: Plan;
}

/** Converts a Date to an ISO `yyyy-MM-dd` DateOnly string. */
function toDateOnly(value: Date | null): string {
  if (!value) {
    return '';
  }
  const y = value.getFullYear();
  const m = String(value.getMonth() + 1).padStart(2, '0');
  const d = String(value.getDate()).padStart(2, '0');
  return `${y}-${m}-${d}`;
}

@Component({
  selector: 'app-plan-dialog',
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
    <h2 mat-dialog-title>{{ isEdit ? ('planning.planDialog.editTitle' | t) : ('planning.planDialog.newTitle' | t) }}</h2>
    <mat-dialog-content>
      <form [formGroup]="form" class="form">
        <mat-form-field appearance="outline" class="full">
          <mat-label>{{ 'planning.planDialog.periodLabel' | t }}</mat-label>
          <input
            matInput
            formControlName="periodLabel"
            placeholder="FY2026"
            autocomplete="off"
          />
          @if (form.controls.periodLabel.hasError('required') && form.controls.periodLabel.touched) {
            <mat-error>{{ 'planning.planDialog.periodLabelRequired' | t }}</mat-error>
          }
        </mat-form-field>

        <div class="row">
          <mat-form-field appearance="outline">
            <mat-label>{{ 'planning.planDialog.periodStart' | t }}</mat-label>
            <input
              matInput
              [matDatepicker]="startPicker"
              formControlName="periodStart"
            />
            <mat-datepicker-toggle matIconSuffix [for]="startPicker" />
            <mat-datepicker #startPicker />
          </mat-form-field>

          <mat-form-field appearance="outline">
            <mat-label>{{ 'planning.planDialog.periodEnd' | t }}</mat-label>
            <input
              matInput
              [matDatepicker]="endPicker"
              formControlName="periodEnd"
            />
            <mat-datepicker-toggle matIconSuffix [for]="endPicker" />
            <mat-datepicker #endPicker />
          </mat-form-field>
        </div>
      </form>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button matButton type="button" (click)="cancel()">{{ 'planning.common.cancel' | t }}</button>
      <button
        matButton="filled"
        type="button"
        (click)="submit()"
        [disabled]="form.invalid"
      >
        {{ isEdit ? ('planning.common.save' | t) : ('planning.common.create' | t) }}
      </button>
    </mat-dialog-actions>
  `,
  styles: `
    .form {
      display: flex;
      flex-direction: column;
      min-width: 400px;
    }
    .row {
      display: flex;
      gap: 1rem;
    }
    .row mat-form-field {
      flex: 1;
    }
    .full {
      width: 100%;
    }
    @media (max-width: 520px) {
      .form {
        min-width: auto;
      }
      .row {
        flex-direction: column;
      }
    }
  `,
})
export class PlanDialogComponent {
  readonly data = inject<PlanDialogData>(MAT_DIALOG_DATA);
  private readonly dialogRef =
    inject<MatDialogRef<PlanDialogComponent, SavePlanRequest>>(MatDialogRef);
  private readonly fb = inject(FormBuilder);

  readonly isEdit = !!this.data.plan;

  readonly form = this.fb.nonNullable.group({
    periodLabel: [
      this.data.plan?.periodLabel ?? '',
      [Validators.required, Validators.maxLength(120)],
    ],
    periodStart: [
      this.data.plan ? new Date(this.data.plan.periodStart) : (null as Date | null),
      [Validators.required],
    ],
    periodEnd: [
      this.data.plan ? new Date(this.data.plan.periodEnd) : (null as Date | null),
      [Validators.required],
    ],
  });

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const v = this.form.getRawValue();
    this.dialogRef.close({
      periodLabel: v.periodLabel.trim(),
      periodStart: toDateOnly(v.periodStart),
      periodEnd: toDateOnly(v.periodEnd),
    });
  }

  cancel(): void {
    this.dialogRef.close();
  }
}
