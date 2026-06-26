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
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';

export interface RejectActionDialogData {
  actionLabel: string;
}

const MIN_REASON = 20;

@Component({
  selector: 'app-reject-action-dialog',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
  ],
  template: `
    <h2 mat-dialog-title>Reject change</h2>
    <mat-dialog-content>
      <p class="hint">
        Provide a reason for rejecting <strong>{{ data.actionLabel }}</strong>. This is
        recorded in the audit trail (minimum {{ minReason }} characters).
      </p>
      <form [formGroup]="form">
        <mat-form-field appearance="outline" class="full">
          <mat-label>Reason</mat-label>
          <textarea
            matInput
            formControlName="reason"
            rows="4"
            [attr.minlength]="minReason"
          ></textarea>
          <mat-hint align="end">{{ form.controls.reason.value.length }} characters</mat-hint>
          @if (form.controls.reason.hasError('required') && form.controls.reason.touched) {
            <mat-error>A reason is required.</mat-error>
          }
          @if (form.controls.reason.hasError('minlength') && form.controls.reason.touched) {
            <mat-error>Reason must be at least {{ minReason }} characters.</mat-error>
          }
        </mat-form-field>
      </form>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button matButton type="button" (click)="cancel()">Cancel</button>
      <button
        matButton="filled"
        type="button"
        class="destructive"
        (click)="submit()"
        [disabled]="form.invalid"
      >
        Reject
      </button>
    </mat-dialog-actions>
  `,
  styles: `
    .full {
      width: 100%;
      min-width: 380px;
    }
    .hint {
      color: var(--mat-sys-on-surface-variant);
      margin-top: 0;
    }
    .destructive {
      --mat-filled-button-container-color: var(--mat-sys-error);
      --mat-filled-button-label-text-color: var(--mat-sys-on-error);
    }
    @media (max-width: 480px) {
      .full {
        min-width: auto;
      }
    }
  `,
})
export class RejectActionDialogComponent {
  readonly data = inject<RejectActionDialogData>(MAT_DIALOG_DATA);
  private readonly dialogRef =
    inject<MatDialogRef<RejectActionDialogComponent, string>>(MatDialogRef);
  private readonly fb = inject(FormBuilder);

  readonly minReason = MIN_REASON;

  readonly form = this.fb.nonNullable.group({
    reason: [
      '',
      [Validators.required, Validators.minLength(MIN_REASON)],
    ],
  });

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    this.dialogRef.close(this.form.getRawValue().reason.trim());
  }

  cancel(): void {
    this.dialogRef.close();
  }
}
