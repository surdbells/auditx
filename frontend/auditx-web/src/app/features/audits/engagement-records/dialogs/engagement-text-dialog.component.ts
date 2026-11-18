import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { FormControl, ReactiveFormsModule, ValidatorFn, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';

import { TranslatePipe } from '../../../../core/i18n/translate.pipe';

export interface EngagementTextDialogData {
  /** i18n keys. */
  title: string;
  hint: string;
  label: string;
  submit: string;
  /** Already-translated line naming what this is about (optional). */
  subject?: string;
  /** Text shown above the field for reference, e.g. the disclosed conflict (optional). */
  quote?: string;
  /** The text to start from (editing a letter). */
  value?: string;
  required: boolean;
  maxLength: number;
  rows?: number;
}

/**
 * One piece of free text for the engagement records: the letter, a manager's safeguards, the auditee's comment on
 * acknowledging. Closes with the trimmed text ('' when an optional field is left empty), or nothing when cancelled.
 */
@Component({
  selector: 'app-engagement-text-dialog',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, MatDialogModule, MatFormFieldModule, MatInputModule, MatButtonModule, TranslatePipe],
  template: `
    <h2 mat-dialog-title>{{ data.title | t }}</h2>
    <mat-dialog-content>
      @if (data.subject) {
        <p class="subject">{{ data.subject }}</p>
      }
      @if (data.quote) {
        <blockquote class="quote">{{ data.quote }}</blockquote>
      }
      <p class="hint">{{ data.hint | t }}</p>
      <mat-form-field appearance="outline" class="full">
        <mat-label>{{ data.label | t }}</mat-label>
        <textarea matInput [formControl]="text" [rows]="data.rows ?? 5" cdkFocusInitial></textarea>
        <mat-hint align="end">{{ text.value.length }} / {{ data.maxLength }}</mat-hint>
        @if ((text.hasError('required') || text.hasError('pattern')) && text.touched) {
          <mat-error>{{ 'engagement.dialog.required' | t }}</mat-error>
        } @else if (text.hasError('maxlength')) {
          <mat-error>{{ 'engagement.dialog.tooLong' | t: { max: data.maxLength } }}</mat-error>
        }
      </mat-form-field>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button matButton type="button" mat-dialog-close>{{ 'common.cancel' | t }}</button>
      <button matButton="filled" type="button" (click)="submit()" [disabled]="text.invalid">{{ data.submit | t }}</button>
    </mat-dialog-actions>
  `,
  styles: `
    .subject {
      margin: 0 0 12px;
      font-weight: 500;
      overflow-wrap: anywhere;
    }
    .quote {
      margin: 0 0 12px;
      padding: 8px 12px;
      border-left: 3px solid var(--mat-sys-outline-variant);
      background: var(--mat-sys-surface-container-low);
      border-radius: 0 6px 6px 0;
      white-space: pre-wrap;
      overflow-wrap: anywhere;
    }
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
export class EngagementTextDialogComponent {
  readonly data = inject<EngagementTextDialogData>(MAT_DIALOG_DATA);
  private readonly dialogRef = inject<MatDialogRef<EngagementTextDialogComponent, string>>(MatDialogRef);

  readonly text = new FormControl(this.data.value ?? '', {
    nonNullable: true,
    validators: [
      Validators.maxLength(this.data.maxLength),
      ...(this.data.required ? ([Validators.required, Validators.pattern(/\S/)] as ValidatorFn[]) : []),
    ],
  });

  submit(): void {
    if (this.text.invalid) {
      this.text.markAsTouched();
      return;
    }
    this.dialogRef.close(this.text.value.trim());
  }
}
