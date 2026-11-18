import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { FormControl, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';

import { TranslatePipe } from '../../../core/i18n/translate.pipe';

/** The server's limit for a review note and its answer. */
export const REVIEW_NOTE_MAX_LENGTH = 4000;

export interface ReviewNoteDialogData {
  /** Raise a new note (a reviewer), or answer an open one (the audit team). */
  mode: 'raise' | 'answer';
  /** The checklist item the note is (or will be) about; null for the audit as a whole. */
  itemPrompt: string | null;
  /** The note being answered (answer mode). */
  noteBody?: string;
}

/** Writes a review note, or the team's answer to one. Closes with the trimmed text, or nothing when cancelled. */
@Component({
  selector: 'app-review-note-dialog',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, MatDialogModule, MatFormFieldModule, MatInputModule, MatButtonModule, TranslatePipe],
  template: `
    <h2 mat-dialog-title>{{ (isAnswer ? 'audits.review.dialog.answerTitle' : 'audits.review.dialog.raiseTitle') | t }}</h2>
    <mat-dialog-content>
      <p class="subject">
        @if (data.itemPrompt) {
          {{ 'audits.review.dialog.onItem' | t: { item: data.itemPrompt } }}
        } @else {
          {{ 'audits.review.dialog.onAudit' | t }}
        }
      </p>
      @if (isAnswer && data.noteBody) {
        <blockquote class="quote">{{ data.noteBody }}</blockquote>
      }
      <p class="hint">{{ (isAnswer ? 'audits.review.dialog.answerHint' : 'audits.review.dialog.raiseHint') | t }}</p>
      <mat-form-field appearance="outline" class="full">
        <mat-label>{{ (isAnswer ? 'audits.review.dialog.answerLabel' : 'audits.review.dialog.noteLabel') | t }}</mat-label>
        <textarea matInput [formControl]="text" rows="5" cdkFocusInitial></textarea>
        <mat-hint align="end">{{ text.value.length }} / {{ max }}</mat-hint>
        @if ((text.hasError('required') || text.hasError('pattern')) && text.touched) {
          <mat-error>{{ 'audits.review.dialog.required' | t }}</mat-error>
        } @else if (text.hasError('maxlength')) {
          <mat-error>{{ 'audits.review.dialog.tooLong' | t: { max: max } }}</mat-error>
        }
      </mat-form-field>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button matButton type="button" mat-dialog-close>{{ 'common.cancel' | t }}</button>
      <button matButton="filled" type="button" (click)="submit()" [disabled]="text.invalid">
        {{ (isAnswer ? 'audits.review.action.sendAnswer' : 'audits.review.action.raise') | t }}
      </button>
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
export class ReviewNoteDialogComponent {
  readonly data = inject<ReviewNoteDialogData>(MAT_DIALOG_DATA);
  private readonly dialogRef = inject<MatDialogRef<ReviewNoteDialogComponent, string>>(MatDialogRef);

  readonly isAnswer = this.data.mode === 'answer';
  readonly max = REVIEW_NOTE_MAX_LENGTH;

  readonly text = new FormControl('', {
    nonNullable: true,
    validators: [Validators.required, Validators.maxLength(REVIEW_NOTE_MAX_LENGTH), Validators.pattern(/\S/)],
  });

  submit(): void {
    if (this.text.invalid) {
      this.text.markAsTouched();
      return;
    }
    this.dialogRef.close(this.text.value.trim());
  }
}
