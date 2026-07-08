import {
  ChangeDetectionStrategy,
  Component,
  inject,
  signal,
} from '@angular/core';
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
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';

import {
  CreateIntegrationRequest,
  INTEGRATION_TYPES,
  Integration,
  IntegrationType,
  UpdateIntegrationRequest,
} from '../../../../core/models';
import { humaniseIntegrationType } from '../integration-type-label';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';

export interface IntegrationEditorDialogData {
  /** Present when editing; absent for create. */
  integration?: Integration;
  /** Other integrations selectable as a fallback (excludes the one being edited). */
  candidates: Integration[];
}

export type IntegrationEditorResult =
  | { mode: 'create'; body: CreateIntegrationRequest }
  | { mode: 'edit'; id: string; body: UpdateIntegrationRequest };

/** Validator: a non-empty value must parse as JSON. */
function jsonValidator(control: AbstractControl): ValidationErrors | null {
  const value = (control.value ?? '').trim();
  if (!value) {
    return null;
  }
  try {
    JSON.parse(value);
    return null;
  } catch {
    return { json: true };
  }
}

@Component({
  selector: 'app-integration-editor-dialog',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatButtonModule,
    MatIconModule,
    TranslatePipe,
  ],
  templateUrl: './integration-editor-dialog.component.html',
  styleUrl: './integration-editor-dialog.component.scss',
})
export class IntegrationEditorDialogComponent {
  readonly data = inject<IntegrationEditorDialogData>(MAT_DIALOG_DATA);
  private readonly dialogRef =
    inject<MatDialogRef<IntegrationEditorDialogComponent, IntegrationEditorResult>>(
      MatDialogRef,
    );
  private readonly fb = inject(FormBuilder);

  readonly types: IntegrationType[] = INTEGRATION_TYPES;
  readonly humaniseType = humaniseIntegrationType;
  readonly isEdit = !!this.data.integration;
  readonly candidates = this.data.candidates;

  /** True while a credential value is already stored (edit mode). */
  readonly hasStoredCredentials = !!this.data.integration?.hasCredentials;
  readonly editingCredentials = signal(!this.isEdit);

  readonly form = this.fb.nonNullable.group({
    type: [
      (this.data.integration?.type ?? 'smtp') as IntegrationType,
      Validators.required,
    ],
    name: [
      this.data.integration?.name ?? '',
      [Validators.required, Validators.maxLength(160)],
    ],
    connectionDetailsJson: [
      this.data.integration?.connectionDetailsJson ?? '',
      [jsonValidator],
    ],
    credentials: [''],
    timeoutSeconds: [
      this.data.integration?.timeoutSeconds ?? 30,
      [Validators.required, Validators.min(1), Validators.max(3600)],
    ],
    fallbackIntegrationId: [this.data.integration?.fallbackIntegrationId ?? ''],
    isActive: [this.data.integration?.isActive ?? true],
  });

  startEditingCredentials(): void {
    this.editingCredentials.set(true);
  }

  cancelEditingCredentials(): void {
    this.editingCredentials.set(false);
    this.form.controls.credentials.setValue('');
  }

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const v = this.form.getRawValue();
    const credentials = this.editingCredentials()
      ? v.credentials.trim() || null
      : null;
    const fallbackIntegrationId = v.fallbackIntegrationId || null;
    const connectionDetailsJson = v.connectionDetailsJson.trim();

    if (this.isEdit && this.data.integration) {
      const body: UpdateIntegrationRequest = {
        name: v.name.trim(),
        connectionDetailsJson,
        credentials,
        timeoutSeconds: v.timeoutSeconds,
        fallbackIntegrationId,
        isActive: v.isActive,
      };
      this.dialogRef.close({ mode: 'edit', id: this.data.integration.id, body });
      return;
    }

    const body: CreateIntegrationRequest = {
      type: v.type,
      name: v.name.trim(),
      connectionDetailsJson,
      credentials,
      timeoutSeconds: v.timeoutSeconds,
      fallbackIntegrationId,
    };
    this.dialogRef.close({ mode: 'create', body });
  }

  cancel(): void {
    this.dialogRef.close();
  }
}
