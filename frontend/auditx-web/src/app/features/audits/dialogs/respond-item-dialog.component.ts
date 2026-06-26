import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import {
  FormBuilder,
  ReactiveFormsModule,
  ValidationErrors,
  ValidatorFn,
  Validators,
} from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import {
  MAT_DIALOG_DATA,
  MatDialogModule,
  MatDialogRef,
} from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatRadioModule } from '@angular/material/radio';

import { ChecklistResponse, ResponseVerdict } from '../../../core/models';

export interface RespondItemDialogData {
  /** The checklist item prompt, for context. */
  prompt: string;
  /** The current draft / submitted response, when one exists. */
  current?: ChecklistResponse | null;
}

export interface RespondItemDialogResult {
  verdict: ResponseVerdict | null;
  comment: string | null;
  /** True = Save draft, false = Submit. */
  isDraft: boolean;
}

/**
 * A comment is required when the verdict is Fail or N/A. This validator runs on
 * the group so it can read the sibling verdict control.
 */
const failNaNeedsComment: ValidatorFn = (group): ValidationErrors | null => {
  const verdict = group.get('verdict')?.value as ResponseVerdict | null;
  const comment = ((group.get('comment')?.value as string) ?? '').trim();
  if ((verdict === 'fail' || verdict === 'na') && !comment) {
    return { commentRequired: true };
  }
  return null;
};

@Component({
  selector: 'app-respond-item-dialog',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatRadioModule,
    MatButtonModule,
  ],
  template: `
    <h2 mat-dialog-title>Respond</h2>
    <mat-dialog-content>
      <p class="prompt">{{ data.prompt }}</p>
      <form [formGroup]="form" class="form">
        <mat-radio-group formControlName="verdict" class="verdicts">
          <mat-radio-button value="pass">Pass</mat-radio-button>
          <mat-radio-button value="fail">Fail</mat-radio-button>
          <mat-radio-button value="na">N/A</mat-radio-button>
        </mat-radio-group>

        <mat-form-field appearance="outline" class="full">
          <mat-label>Comment{{ commentRequired() ? '' : ' (optional)' }}</mat-label>
          <textarea matInput formControlName="comment" rows="3"></textarea>
          @if (form.hasError('commentRequired') && form.controls.comment.touched) {
            <mat-error>A comment is required for Fail or N/A.</mat-error>
          }
        </mat-form-field>
      </form>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button matButton type="button" (click)="cancel()">Cancel</button>
      <button matButton type="button" (click)="saveDraft()">Save draft</button>
      <button
        matButton="filled"
        type="button"
        (click)="submit()"
        [disabled]="!form.controls.verdict.value || form.invalid"
      >
        Submit
      </button>
    </mat-dialog-actions>
  `,
  styles: `
    .prompt {
      margin: 0 0 1rem;
      font-weight: 500;
    }
    .form {
      display: flex;
      flex-direction: column;
      min-width: 420px;
      gap: 0.75rem;
    }
    .verdicts {
      display: flex;
      gap: 1.25rem;
      flex-wrap: wrap;
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
export class RespondItemDialogComponent {
  readonly data = inject<RespondItemDialogData>(MAT_DIALOG_DATA);
  private readonly dialogRef = inject<
    MatDialogRef<RespondItemDialogComponent, RespondItemDialogResult>
  >(MatDialogRef);
  private readonly fb = inject(FormBuilder);

  readonly form = this.fb.nonNullable.group(
    {
      verdict: [
        (this.data.current?.verdict ?? null) as ResponseVerdict | null,
      ],
      comment: [this.data.current?.comment ?? ''],
    },
    { validators: [failNaNeedsComment] },
  );

  /** True when the chosen verdict makes a comment mandatory. */
  commentRequired(): boolean {
    const v = this.form.controls.verdict.value;
    return v === 'fail' || v === 'na';
  }

  saveDraft(): void {
    // Drafts may be partial; only the comment-for-fail/na rule still applies.
    if (this.form.hasError('commentRequired')) {
      this.form.markAllAsTouched();
      return;
    }
    this.close(true);
  }

  submit(): void {
    if (!this.form.controls.verdict.value || this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    this.close(false);
  }

  private close(isDraft: boolean): void {
    const v = this.form.getRawValue();
    this.dialogRef.close({
      verdict: v.verdict,
      comment: v.comment.trim() || null,
      isDraft,
    });
  }

  cancel(): void {
    this.dialogRef.close();
  }
}
