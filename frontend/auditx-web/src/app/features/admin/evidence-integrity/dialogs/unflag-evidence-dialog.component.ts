import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import {
  AbstractControl,
  FormBuilder,
  ReactiveFormsModule,
  ValidationErrors,
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

import { FlaggedEvidence } from '../../../../core/models';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';

/** Rejects empty / whitespace-only resolution notes. */
function nonBlankValidator(
  control: AbstractControl,
): ValidationErrors | null {
  return (control.value ?? '').trim() ? null : { required: true };
}

export interface UnflagEvidenceDialogData {
  evidence: FlaggedEvidence;
}

/** Dialog result: the mandatory resolution note. */
export interface UnflagEvidenceDialogResult {
  resolution: string;
}

@Component({
  selector: 'app-unflag-evidence-dialog',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    TranslatePipe,
  ],
  templateUrl: './unflag-evidence-dialog.component.html',
  styleUrl: './unflag-evidence-dialog.component.scss',
})
export class UnflagEvidenceDialogComponent {
  readonly data = inject<UnflagEvidenceDialogData>(MAT_DIALOG_DATA);
  private readonly dialogRef =
    inject<
      MatDialogRef<UnflagEvidenceDialogComponent, UnflagEvidenceDialogResult>
    >(MatDialogRef);
  private readonly fb = inject(FormBuilder);

  readonly evidence = this.data.evidence;

  readonly form = this.fb.nonNullable.group({
    resolution: ['', [Validators.required, nonBlankValidator]],
  });

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    this.dialogRef.close({
      resolution: this.form.getRawValue().resolution.trim(),
    });
  }

  cancel(): void {
    this.dialogRef.close();
  }
}
