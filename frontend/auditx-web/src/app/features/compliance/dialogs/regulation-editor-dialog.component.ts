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

import {
  RegisterRegulationRequest,
  Regulation,
  UpdateRegulationRequest,
} from '../../../core/models';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';

export interface RegulationEditorDialogData {
  regulation?: Regulation;
}

export type RegulationEditorResult =
  | { mode: 'register'; body: RegisterRegulationRequest }
  | { mode: 'update'; id: string; body: UpdateRegulationRequest };

@Component({
  selector: 'app-regulation-editor-dialog',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    TranslatePipe,
  ],
  templateUrl: './regulation-editor-dialog.component.html',
  styleUrl: './regulation-editor-dialog.component.scss',
})
export class RegulationEditorDialogComponent {
  readonly data = inject<RegulationEditorDialogData>(MAT_DIALOG_DATA);
  private readonly dialogRef = inject<MatDialogRef<RegulationEditorDialogComponent, RegulationEditorResult>>(MatDialogRef);
  private readonly fb = inject(FormBuilder);

  readonly isEdit = !!this.data.regulation;

  readonly form = this.fb.nonNullable.group({
    code: [this.data.regulation?.code ?? '', [Validators.required, Validators.maxLength(50)]],
    name: [this.data.regulation?.name ?? '', [Validators.required, Validators.maxLength(300)]],
    authority: [this.data.regulation?.authority ?? '', Validators.maxLength(200)],
    category: [this.data.regulation?.category ?? '', Validators.maxLength(100)],
    description: [this.data.regulation?.description ?? ''],
  });

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const v = this.form.getRawValue();
    const common = {
      name: v.name.trim(),
      authority: v.authority.trim() || null,
      description: v.description.trim() || null,
      category: v.category.trim() || null,
    };

    if (this.isEdit && this.data.regulation) {
      this.dialogRef.close({
        mode: 'update',
        id: this.data.regulation.id,
        body: { ...common, version: this.data.regulation.version },
      });
    } else {
      this.dialogRef.close({
        mode: 'register',
        body: { code: v.code.trim(), ...common },
      });
    }
  }

  cancel(): void {
    this.dialogRef.close();
  }
}
