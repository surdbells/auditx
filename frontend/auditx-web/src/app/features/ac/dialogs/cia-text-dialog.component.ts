import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import {
  MAT_DIALOG_DATA,
  MatDialogModule,
  MatDialogRef,
} from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';

import { TranslatePipe } from '../../../core/i18n/translate.pipe';

export interface CiaTextDialogData {
  /** Current supplementary text (prefills the editor). */
  supplementaryText: string | null;
}

/**
 * Edit the CIA supplementary narrative attached to an AC pack. The dialog
 * returns the new text (may be empty) so the caller can PATCH it.
 */
@Component({
  selector: 'app-cia-text-dialog',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    TranslatePipe,
  ],
  template: `
    <h2 mat-dialog-title>{{ 'ac.ciaText.title' | t }}</h2>
    <mat-dialog-content>
      <form [formGroup]="form" class="form">
        <mat-form-field appearance="outline" class="full">
          <mat-label>{{ 'ac.ciaText.label' | t }}</mat-label>
          <textarea
            matInput
            formControlName="supplementaryText"
            rows="8"
            [placeholder]="'ac.ciaText.placeholder' | t"
          ></textarea>
        </mat-form-field>
      </form>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button matButton type="button" (click)="cancel()">{{ 'ac.common.cancel' | t }}</button>
      <button matButton="filled" type="button" (click)="submit()">{{ 'ac.common.save' | t }}</button>
    </mat-dialog-actions>
  `,
  styles: `
    .form {
      display: flex;
      flex-direction: column;
      min-width: 460px;
    }
    .full {
      width: 100%;
    }
    @media (max-width: 560px) {
      .form {
        min-width: auto;
      }
    }
  `,
})
export class CiaTextDialogComponent {
  readonly data = inject<CiaTextDialogData>(MAT_DIALOG_DATA);
  private readonly dialogRef =
    inject<MatDialogRef<CiaTextDialogComponent, string>>(MatDialogRef);
  private readonly fb = inject(FormBuilder);

  readonly form = this.fb.nonNullable.group({
    supplementaryText: [this.data.supplementaryText ?? ''],
  });

  submit(): void {
    this.dialogRef.close(this.form.getRawValue().supplementaryText);
  }

  cancel(): void {
    this.dialogRef.close();
  }
}