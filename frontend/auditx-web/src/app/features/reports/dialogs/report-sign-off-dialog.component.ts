import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { FormControl, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';

import { TranslatePipe } from '../../../core/i18n/translate.pipe';

export interface ReportSignOffDialogData {
  /** Sign the final off (an optional remark), or return it for changes (a reason is required). */
  mode: 'sign_off' | 'return';
  versionNumber: number;
}

/** The Chief Internal Auditor's decision on a final report: sign it off, or return it saying what must change. */
@Component({
  selector: 'app-report-sign-off-dialog',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, MatDialogModule, MatFormFieldModule, MatInputModule, MatButtonModule, TranslatePipe],
  template: `
    <h2 mat-dialog-title>
      {{ (isReturn ? 'reports.review.returnTitle' : 'reports.review.signOffTitle') | t: { version: data.versionNumber } }}
    </h2>
    <mat-dialog-content>
      <p class="hint">{{ (isReturn ? 'reports.review.returnHint' : 'reports.review.signOffHint') | t }}</p>
      <mat-form-field appearance="outline" class="full">
        <mat-label>{{ (isReturn ? 'reports.review.returnReason' : 'reports.review.signOffNote') | t }}</mat-label>
        <textarea matInput [formControl]="text" rows="4" cdkFocusInitial></textarea>
        @if (text.hasError('required') && text.touched) {
          <mat-error>{{ 'reports.review.returnReasonRequired' | t }}</mat-error>
        } @else if (text.hasError('minlength') && text.touched) {
          <mat-error>{{ 'reports.review.returnReasonShort' | t }}</mat-error>
        }
      </mat-form-field>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button matButton type="button" mat-dialog-close>{{ 'common.cancel' | t }}</button>
      <button matButton="filled" type="button" (click)="submit()" [disabled]="text.invalid">
        {{ (isReturn ? 'reports.review.action.return' : 'reports.review.action.signOff') | t }}
      </button>
    </mat-dialog-actions>
  `,
  styles: `
    .hint {
      margin: 0 0 16px;
      color: var(--mat-sys-on-surface-variant);
      font-size: 0.875rem;
      line-height: 1.45;
    }
    .full {
      width: 100%;
    }
  `,
})
export class ReportSignOffDialogComponent {
  readonly data = inject<ReportSignOffDialogData>(MAT_DIALOG_DATA);
  private readonly dialogRef = inject<MatDialogRef<ReportSignOffDialogComponent, string | null>>(MatDialogRef);

  readonly isReturn = this.data.mode === 'return';

  readonly text = new FormControl('', {
    nonNullable: true,
    validators: this.isReturn
      ? [Validators.required, Validators.minLength(10), Validators.maxLength(2000)]
      : [Validators.maxLength(2000)],
  });

  submit(): void {
    if (this.text.invalid) {
      this.text.markAsTouched();
      return;
    }
    const value = this.text.value.trim();
    this.dialogRef.close(value.length ? value : null);
  }
}
