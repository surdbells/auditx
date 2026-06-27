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

export interface ExceptionReasonDialogData {
  title: string;
  message?: string;
  label?: string;
  /** Whether a reason is mandatory (defaults to true). */
  reasonRequired?: boolean;
  confirmLabel?: string;
  /** When true, the confirm button is styled as a destructive (warn) action. */
  destructive?: boolean;
}

export interface ExceptionReasonResult {
  reason: string;
}

/**
 * Generic reason-capture dialog used by reject / return-for-evidence / cancel
 * and the optional closure note.
 */
@Component({
  selector: 'app-exception-reason-dialog',
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
          <mat-label>{{ label }}{{ reasonRequired ? '' : ' (optional)' }}</mat-label>
          <textarea matInput formControlName="reason" rows="3"></textarea>
          @if (form.controls.reason.hasError('required') && form.controls.reason.touched) {
            <mat-error>This field is required.</mat-error>
          }
        </mat-form-field>
      </form>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button matButton type="button" (click)="cancel()">Cancel</button>
      <button
        matButton="filled"
        type="button"
        [class.destructive]="data.destructive"
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
    .destructive {
      --mat-filled-button-container-color: var(--mat-sys-error);
      --mat-filled-button-label-text-color: var(--mat-sys-on-error);
    }
    @media (max-width: 520px) {
      .form {
        min-width: auto;
      }
    }
  `,
})
export class ExceptionReasonDialogComponent {
  readonly data = inject<ExceptionReasonDialogData>(MAT_DIALOG_DATA);
  private readonly dialogRef = inject<
    MatDialogRef<ExceptionReasonDialogComponent, ExceptionReasonResult>
  >(MatDialogRef);
  private readonly fb = inject(FormBuilder);

  readonly reasonRequired = this.data.reasonRequired ?? true;
  readonly label = this.data.label ?? 'Reason';

  readonly form = this.fb.nonNullable.group({
    reason: ['', this.reasonRequired ? [Validators.required] : []],
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
