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

export interface ConfigReasonDialogData {
  title: string;
  message?: string;
  label?: string;
  /** Minimum reason length (defaults to 20). */
  minLength?: number;
  confirmLabel?: string;
}

export interface ConfigReasonResult {
  changeReason: string;
}

/**
 * Free-text change-reason dialog used by Activate / Rollback. Requires a reason
 * of at least `minLength` characters (default 20). Mirrors the sanctions reason
 * dialog.
 */
@Component({
  selector: 'app-config-reason-dialog',
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
          <textarea matInput formControlName="changeReason" rows="3"></textarea>
          @if (
            form.controls.changeReason.hasError('required') &&
            form.controls.changeReason.touched
          ) {
            <mat-error>This field is required.</mat-error>
          }
          @if (form.controls.changeReason.hasError('minlength')) {
            <mat-error
              >At least {{ minLength }} characters are required.</mat-error
            >
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
export class ConfigReasonDialogComponent {
  readonly data = inject<ConfigReasonDialogData>(MAT_DIALOG_DATA);
  private readonly dialogRef = inject<
    MatDialogRef<ConfigReasonDialogComponent, ConfigReasonResult>
  >(MatDialogRef);
  private readonly fb = inject(FormBuilder);

  readonly label = this.data.label ?? 'Change reason';
  readonly minLength = this.data.minLength ?? 20;

  readonly form = this.fb.nonNullable.group({
    changeReason: [
      '',
      [Validators.required, Validators.minLength(this.minLength)],
    ],
  });

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    this.dialogRef.close({
      changeReason: this.form.getRawValue().changeReason.trim(),
    });
  }

  cancel(): void {
    this.dialogRef.close();
  }
}
