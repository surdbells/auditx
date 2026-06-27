import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import {
  MAT_DIALOG_DATA,
  MatDialogModule,
  MatDialogRef,
} from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';

export interface SanctionsReasonDialogData {
  title: string;
  message?: string;
  label?: string;
  /** Minimum reason length (defaults to 0 = no minimum beyond required). */
  minLength?: number;
  confirmLabel?: string;
}

export interface SanctionsReasonResult {
  reason: string;
}

/**
 * Generic free-text reason dialog used by Refer to DC (min 20 chars) and File
 * appeal (basis). Mirrors the exceptions reason dialog, adding an optional
 * minimum-length constraint.
 */
@Component({
  selector: 'app-sanctions-reason-dialog',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
  ],
  template: `
    <h2 mat-dialog-title>{{ data.title }}</h2>
    <mat-dialog-content>
      @if (data.message) {
        <p>{{ data.message }}</p>
      }
      <form [formGroup]="form" class="form">
        <mat-form-field appearance="outline" class="full">
          <mat-label>{{ label }}</mat-label>
          <textarea matInput formControlName="reason" rows="3"></textarea>
          @if (
            form.controls.reason.hasError('required') &&
            form.controls.reason.touched
          ) {
            <mat-error>This field is required.</mat-error>
          }
          @if (form.controls.reason.hasError('minlength')) {
            <mat-error>At least {{ minLength }} characters are required.</mat-error>
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
        {{ data.confirmLabel ?? 'Confirm' }}
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
export class SanctionsReasonDialogComponent {
  readonly data = inject<SanctionsReasonDialogData>(MAT_DIALOG_DATA);
  private readonly dialogRef = inject<
    MatDialogRef<SanctionsReasonDialogComponent, SanctionsReasonResult>
  >(MatDialogRef);
  private readonly fb = inject(FormBuilder);

  readonly label = this.data.label ?? 'Reason';
  readonly minLength = this.data.minLength ?? 0;

  readonly form = this.fb.nonNullable.group({
    reason: [
      '',
      this.minLength > 0
        ? [Validators.required, Validators.minLength(this.minLength)]
        : [Validators.required],
    ],
  });

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    this.dialogRef.close({ reason: this.form.getRawValue().reason.trim() });
  }

  cancel(): void {
    this.dialogRef.close();
  }
}
