import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
} from '@angular/core';
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
  CONTROL_FREQUENCIES,
  CONTROL_TYPES,
  Control,
  ControlEffectiveness,
  ControlFrequency,
  ControlType,
  RegisterControlRequest,
  UpdateControlRequest,
} from '../../../core/models';
import { UserLookupService } from '../../../core/services/user-lookup.service';
import { EntityLookupService } from '../../../core/services/entity-lookup.service';
import { NotificationService } from '../../../core/services/notification.service';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';
import { TranslationService } from '../../../core/i18n/translation.service';
import {
  SearchableSelectComponent,
  SelectOption,
} from '../../../shared/components/searchable-select/searchable-select.component';

export interface ControlEditorDialogData {
  control?: Control;
}

export type ControlEditorResult =
  | { mode: 'register'; body: RegisterControlRequest }
  | { mode: 'update'; id: string; body: UpdateControlRequest };

@Component({
  selector: 'app-control-editor-dialog',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatButtonModule,
    TranslatePipe,
    SearchableSelectComponent,
  ],
  templateUrl: './control-editor-dialog.component.html',
  styleUrl: './control-editor-dialog.component.scss',
})
export class ControlEditorDialogComponent {
  readonly data = inject<ControlEditorDialogData>(MAT_DIALOG_DATA);
  private readonly dialogRef = inject<MatDialogRef<ControlEditorDialogComponent, ControlEditorResult>>(MatDialogRef);
  private readonly fb = inject(FormBuilder);
  readonly userLookup = inject(UserLookupService);
  readonly entityLookup = inject(EntityLookupService);
  private readonly notify = inject(NotificationService);
  private readonly i18n = inject(TranslationService);

  readonly isEdit = !!this.data.control;
  readonly types = CONTROL_TYPES;
  readonly frequencies = CONTROL_FREQUENCIES;
  readonly effectivenessOptions = CONTROL_EFFECTIVENESS;

  readonly ownerOptions = computed<SelectOption[]>(() =>
    this.userLookup.options().map((u) => ({ value: u.id, label: u.displayName })),
  );
  readonly entityOptions = computed<SelectOption[]>(() =>
    this.entityLookup.options().map((e) => ({ value: e.id, label: e.name })),
  );

  readonly form = this.fb.nonNullable.group({
    code: [this.data.control?.code ?? '', [Validators.required, Validators.maxLength(50)]],
    title: [this.data.control?.title ?? '', [Validators.required, Validators.maxLength(300)]],
    description: [this.data.control?.description ?? ''],
    controlType: [this.data.control?.controlType ?? ('preventive' as ControlType), Validators.required],
    frequency: [this.data.control?.frequency ?? ('continuous' as ControlFrequency), Validators.required],
    ownerUserId: [this.data.control?.ownerUserId ?? (null as string | null), Validators.required],
    auditableEntityId: [this.data.control?.auditableEntityId ?? (null as string | null)],
    effectiveness: [this.data.control?.effectiveness ?? ('not_tested' as ControlEffectiveness)],
    lastTestedDate: [this.data.control?.lastTestedDate ?? ''],
  });

  constructor() {
    this.userLookup.ensureLoaded();
    this.entityLookup.ensureLoaded();
  }

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const v = this.form.getRawValue();

    if (this.isEdit && v.effectiveness !== 'not_tested' && !v.lastTestedDate) {
      this.notify.warning(this.i18n.translate('control.warn.testedDateRequired'));
      return;
    }

    if (this.isEdit && this.data.control) {
      this.dialogRef.close({
        mode: 'update',
        id: this.data.control.id,
        body: {
          title: v.title.trim(),
          description: v.description.trim() || null,
          controlType: v.controlType,
          frequency: v.frequency,
          ownerUserId: v.ownerUserId!,
          auditableEntityId: v.auditableEntityId || null,
          effectiveness: v.effectiveness,
          lastTestedDate: v.effectiveness === 'not_tested' ? null : v.lastTestedDate || null,
          version: this.data.control.version,
        },
      });
    } else {
      this.dialogRef.close({
        mode: 'register',
        body: {
          code: v.code.trim(),
          title: v.title.trim(),
          description: v.description.trim() || null,
          controlType: v.controlType,
          frequency: v.frequency,
          ownerUserId: v.ownerUserId!,
          auditableEntityId: v.auditableEntityId || null,
        },
      });
    }
  }

  cancel(): void {
    this.dialogRef.close();
  }
}
