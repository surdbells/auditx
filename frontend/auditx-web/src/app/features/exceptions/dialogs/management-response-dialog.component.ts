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
import { MatSelectModule } from '@angular/material/select';

import { MANAGEMENT_RESPONSE_DECISIONS, ManagementResponseDecision } from '../../../core/models';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';

export interface ManagementResponseDialogData {
  decision?: ManagementResponseDecision | null;
  comment?: string | null;
}

export interface ManagementResponseDialogResult {
  decision: ManagementResponseDecision;
  comment: string;
}

/** Records management's formal position on a finding (P2-B). */
@Component({
  selector: 'app-management-response-dialog',
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
    <h2 mat-dialog-title>{{ 'exceptions.dialog.managementResponseTitle' | t }}</h2>
    <mat-dialog-content>
      <form [formGroup]="form" class="form">
        <mat-form-field appearance="outline" class="full">
          <mat-label>{{ 'exceptions.field.responseDecision' | t }}</mat-label>
          <mat-select formControlName="decision">
            @for (d of decisions; track d) {
              <mat-option [value]="d">{{ 'exceptions.responseDecision.' + d | t }}</mat-option>
            }
          </mat-select>
        </mat-form-field>
        <mat-form-field appearance="outline" class="full">
          <mat-label>{{ 'exceptions.field.responseComment' | t }}</mat-label>
          <textarea matInput formControlName="comment" rows="4"></textarea>
          @if (form.controls.comment.hasError('required') && form.controls.comment.touched) {
            <mat-error>{{ 'exceptions.error.responseCommentRequired' | t }}</mat-error>
          }
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
export class ManagementResponseDialogComponent {
  readonly data = inject<ManagementResponseDialogData>(MAT_DIALOG_DATA);
  private readonly dialogRef = inject<MatDialogRef<ManagementResponseDialogComponent, ManagementResponseDialogResult>>(MatDialogRef);
  private readonly fb = inject(FormBuilder);

  readonly decisions = MANAGEMENT_RESPONSE_DECISIONS;

  readonly form = this.fb.nonNullable.group({
    decision: [this.data.decision ?? ('accepted' as ManagementResponseDecision), [Validators.required]],
    comment: [this.data.comment ?? '', [Validators.required, Validators.maxLength(2000)]],
  });

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const v = this.form.getRawValue();
    this.dialogRef.close({ decision: v.decision, comment: v.comment.trim() });
  }

  cancel(): void {
    this.dialogRef.close();
  }
}
