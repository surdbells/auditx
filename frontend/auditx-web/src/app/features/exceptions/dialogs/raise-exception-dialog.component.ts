import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import {
  MAT_DIALOG_DATA,
  MatDialogModule,
  MatDialogRef,
} from '@angular/material/dialog';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { provideNativeDateAdapter } from '@angular/material/core';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';

import { ReferenceDataLookupService } from '../../../core/services/reference-data-lookup.service';
import {
  ExceptionSeverity,
  RaiseExceptionRequest,
  UserDto,
} from '../../../core/models';

export interface RaiseExceptionDialogData {
  /** Checklist item this exception is raised against. */
  checklistItemId: string;
  /** Prefilled title (← item prompt). */
  title?: string;
  /** Prefilled root cause / recommendation (← the item's response comment). */
  rootCause?: string;
  recommendation?: string;
  /** Users selectable as the exception owner. */
  users: UserDto[];
}

const SEVERITIES: { value: ExceptionSeverity; label: string }[] = [
  { value: 'low', label: 'Low' },
  { value: 'medium', label: 'Medium' },
  { value: 'high', label: 'High' },
  { value: 'critical', label: 'Critical' },
];

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
  selector: 'app-raise-exception-dialog',
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [provideNativeDateAdapter()],
  imports: [
    ReactiveFormsModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatDatepickerModule,
    MatCheckboxModule,
    MatButtonModule,
  ],
  template: `
    <h2 mat-dialog-title>Raise exception</h2>
    <mat-dialog-content>
      <form [formGroup]="form" class="form">
        <mat-form-field appearance="outline" class="full">
          <mat-label>Title</mat-label>
          <input matInput formControlName="title" autocomplete="off" />
          @if (form.controls.title.hasError('required') && form.controls.title.touched) {
            <mat-error>A title is required.</mat-error>
          }
        </mat-form-field>

        <div class="row">
          <mat-form-field appearance="outline">
            <mat-label>Severity</mat-label>
            <mat-select formControlName="severity">
              @for (s of severities; track s.value) {
                <mat-option [value]="s.value">{{ s.label }}</mat-option>
              }
            </mat-select>
          </mat-form-field>
          <mat-form-field appearance="outline">
            <mat-label>Owner</mat-label>
            <mat-select formControlName="ownerUserId">
              @for (u of data.users; track u.id) {
                <mat-option [value]="u.id">{{ u.displayName }}</mat-option>
              }
            </mat-select>
            @if (form.controls.ownerUserId.hasError('required') && form.controls.ownerUserId.touched) {
              <mat-error>Select an owner.</mat-error>
            }
          </mat-form-field>
        </div>

        <mat-form-field appearance="outline" class="full">
          <mat-label>Category (optional)</mat-label>
          <mat-select formControlName="category">
            <mat-option [value]="''">— none —</mat-option>
            @for (o of categories(); track o.code) {
              <mat-option [value]="o.code">{{ o.label }}</mat-option>
            }
          </mat-select>
        </mat-form-field>

        <mat-form-field appearance="outline" class="full">
          <mat-label>Root cause</mat-label>
          <textarea matInput formControlName="rootCause" rows="3"></textarea>
          @if (form.controls.rootCause.hasError('required') && form.controls.rootCause.touched) {
            <mat-error>A root cause is required.</mat-error>
          }
        </mat-form-field>

        <mat-form-field appearance="outline" class="full">
          <mat-label>Recommendation</mat-label>
          <textarea matInput formControlName="recommendation" rows="3"></textarea>
          @if (form.controls.recommendation.hasError('required') && form.controls.recommendation.touched) {
            <mat-error>A recommendation is required.</mat-error>
          }
        </mat-form-field>

        <mat-checkbox formControlName="overrideTargetDate">
          Override the default target date
        </mat-checkbox>

        @if (form.controls.overrideTargetDate.value) {
          <div class="row">
            <mat-form-field appearance="outline">
              <mat-label>Target date</mat-label>
              <input matInput [matDatepicker]="picker" formControlName="targetDateOverride" />
              <mat-datepicker-toggle matIconSuffix [for]="picker" />
              <mat-datepicker #picker />
              @if (form.controls.targetDateOverride.hasError('required') && form.controls.targetDateOverride.touched) {
                <mat-error>Provide a target date.</mat-error>
              }
            </mat-form-field>
            <mat-form-field appearance="outline">
              <mat-label>Override rationale</mat-label>
              <input matInput formControlName="overrideRationale" autocomplete="off" />
              @if (form.controls.overrideRationale.hasError('required') && form.controls.overrideRationale.touched) {
                <mat-error>Provide a rationale.</mat-error>
              }
            </mat-form-field>
          </div>
        }
      </form>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button matButton type="button" (click)="cancel()">Cancel</button>
      <button matButton="filled" type="button" (click)="submit()" [disabled]="form.invalid">
        Raise exception
      </button>
    </mat-dialog-actions>
  `,
  styles: `
    .form {
      display: flex;
      flex-direction: column;
      gap: 0.25rem;
      min-width: 480px;
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
    @media (max-width: 560px) {
      .form {
        min-width: auto;
      }
      .row {
        flex-direction: column;
      }
    }
  `,
})
export class RaiseExceptionDialogComponent {
  readonly data = inject<RaiseExceptionDialogData>(MAT_DIALOG_DATA);
  private readonly dialogRef = inject<
    MatDialogRef<RaiseExceptionDialogComponent, RaiseExceptionRequest>
  >(MatDialogRef);
  private readonly fb = inject(FormBuilder);
  private readonly refLookup = inject(ReferenceDataLookupService);

  readonly severities = SEVERITIES;
  /** Active exception-category reference-data items (lazy-loaded). */
  readonly categories = this.refLookup.options('exception_category');

  readonly form = this.fb.nonNullable.group({
    title: [this.data.title ?? '', [Validators.required, Validators.maxLength(300)]],
    severity: ['medium' as ExceptionSeverity, [Validators.required]],
    ownerUserId: ['', [Validators.required]],
    category: [''],
    rootCause: [this.data.rootCause ?? '', [Validators.required]],
    recommendation: [this.data.recommendation ?? '', [Validators.required]],
    overrideTargetDate: [false],
    targetDateOverride: [null as Date | null],
    overrideRationale: [''],
  });

  constructor() {
    // Make target-date + rationale required only when override is enabled.
    this.form.controls.overrideTargetDate.valueChanges.subscribe((on) => {
      const dateCtrl = this.form.controls.targetDateOverride;
      const rationaleCtrl = this.form.controls.overrideRationale;
      if (on) {
        dateCtrl.addValidators(Validators.required);
        rationaleCtrl.addValidators(Validators.required);
      } else {
        dateCtrl.removeValidators(Validators.required);
        rationaleCtrl.removeValidators(Validators.required);
        dateCtrl.setValue(null);
        rationaleCtrl.setValue('');
      }
      dateCtrl.updateValueAndValidity();
      rationaleCtrl.updateValueAndValidity();
    });
  }

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const v = this.form.getRawValue();
    this.dialogRef.close({
      checklistItemId: this.data.checklistItemId,
      title: v.title.trim(),
      severity: v.severity,
      rootCause: v.rootCause.trim(),
      recommendation: v.recommendation.trim(),
      category: v.category.trim() || null,
      ownerUserId: v.ownerUserId,
      targetDateOverride: v.overrideTargetDate
        ? toDateOnly(v.targetDateOverride)
        : null,
      overrideRationale: v.overrideTargetDate
        ? v.overrideRationale.trim() || null
        : null,
    });
  }

  cancel(): void {
    this.dialogRef.close();
  }
}
