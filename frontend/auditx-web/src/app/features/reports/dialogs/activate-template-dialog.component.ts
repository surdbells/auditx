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

export interface ActivateTemplateDialogData {
  /** Template name shown in the prompt. */
  templateName: string;
  versionNumber: number;
}

export interface ActivateTemplateDialogResult {
  reason: string;
}

const MIN_REASON = 20;

/** Activate a report-template version. Requires a reason of >= 20 chars. */
@Component({
  selector: 'app-activate-template-dialog',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
  ],
  template: `
    <h2 mat-dialog-title>Activate template</h2>
    <mat-dialog-content>
      <p>
        Activate "{{ data.templateName }}" v{{ data.versionNumber }}. Provide an
        activation reason (at least {{ minLength }} characters).
      </p>
      <form [formGroup]="form" class="form">
        <mat-form-field appearance="outline" class="full">
          <mat-label>Activation reason</mat-label>
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
        Activate
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
export class ActivateTemplateDialogComponent {
  readonly data = inject<ActivateTemplateDialogData>(MAT_DIALOG_DATA);
  private readonly dialogRef =
    inject<
      MatDialogRef<
        ActivateTemplateDialogComponent,
        ActivateTemplateDialogResult
      >
    >(MatDialogRef);
  private readonly fb = inject(FormBuilder);

  readonly minLength = MIN_REASON;

  readonly form = this.fb.nonNullable.group({
    reason: ['', [Validators.required, Validators.minLength(MIN_REASON)]],
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
