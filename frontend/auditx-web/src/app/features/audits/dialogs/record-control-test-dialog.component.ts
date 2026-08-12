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

import {
  CONTROL_EFFECTIVENESS,
  ControlEffectiveness,
} from '../../../core/models';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';
import { TranslationService } from '../../../core/i18n/translation.service';

export interface RecordControlTestDialogData {
  /** The checklist-item prompt, for context. */
  prompt: string;
  /** The effectiveness suggested from the item's response (auditor can override). */
  suggested: ControlEffectiveness;
}

export interface RecordControlTestDialogResult {
  result: ControlEffectiveness;
  notes: string | null;
}

/** Confirm/override the effectiveness a control test records from an audit item's response. */
@Component({
  selector: 'app-record-control-test-dialog',
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
    <h2 mat-dialog-title>{{ 'controlTest.dialog.title' | t }}</h2>
    <mat-dialog-content>
      <p class="prompt">{{ data.prompt }}</p>
      <form [formGroup]="form" class="form">
        <mat-form-field appearance="outline" class="full">
          <mat-label>{{ 'controlTest.dialog.result' | t }}</mat-label>
          <mat-select formControlName="result">
            @for (e of effectiveness; track e) {
              <mat-option [value]="e">{{ 'control.effectiveness.' + e | t }}</mat-option>
            }
          </mat-select>
          <mat-hint>{{ 'controlTest.dialog.resultHint' | t }}</mat-hint>
        </mat-form-field>

        <mat-form-field appearance="outline" class="full">
          <mat-label>{{ 'controlTest.dialog.notes' | t }}</mat-label>
          <textarea matInput formControlName="notes" rows="3"></textarea>
        </mat-form-field>
      </form>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button matButton type="button" (click)="cancel()">{{ 'controlTest.dialog.cancel' | t }}</button>
      <button matButton="filled" type="button" (click)="submit()" [disabled]="form.invalid">
        {{ 'controlTest.dialog.record' | t }}
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
      gap: 0.5rem;
    }
    .full {
      width: 100%;
    }
    @media (max-width: 480px) {
      .form {
        min-width: auto;
      }
    }
  `,
})
export class RecordControlTestDialogComponent {
  readonly data = inject<RecordControlTestDialogData>(MAT_DIALOG_DATA);
  private readonly dialogRef =
    inject<MatDialogRef<RecordControlTestDialogComponent, RecordControlTestDialogResult>>(MatDialogRef);
  private readonly fb = inject(FormBuilder);
  readonly i18n = inject(TranslationService);

  /** Only concrete outcomes — a test never records "not tested". */
  readonly effectiveness: ControlEffectiveness[] = CONTROL_EFFECTIVENESS.filter(
    (e) => e !== 'not_tested',
  );

  readonly form = this.fb.nonNullable.group({
    result: [this.data.suggested as ControlEffectiveness, [Validators.required]],
    notes: [''],
  });

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const v = this.form.getRawValue();
    this.dialogRef.close({ result: v.result, notes: v.notes.trim() || null });
  }

  cancel(): void {
    this.dialogRef.close();
  }
}
