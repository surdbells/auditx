import {
  ChangeDetectionStrategy,
  Component,
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

import { CreateOrgUnitRequest, OrgUnit } from '../../../core/models';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';
import {
  SearchableSelectComponent,
  SelectOption,
} from '../../../shared/components/searchable-select/searchable-select.component';

export interface OrgUnitDialogData {
  /** Present when editing; absent for create. */
  unit?: OrgUnit;
  /** Selectable parents (full-path labels); excludes the edited unit itself. */
  parentOptions: SelectOption[];
}

export type OrgUnitDialogResult =
  | { mode: 'create'; body: CreateOrgUnitRequest }
  | {
      mode: 'edit';
      id: string;
      name: string;
      parentOrgUnitId: string | null;
      original: { name: string; parentOrgUnitId: string | null };
    };

/**
 * Create or edit an org unit. The `code` is immutable (create-only) since it is
 * a stable business key; on edit only the name (rename) and parent (reparent)
 * can change — the list component fans those out to the two dedicated endpoints.
 */
@Component({
  selector: 'app-org-unit-dialog',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    TranslatePipe,
    SearchableSelectComponent,
  ],
  template: `
    <h2 mat-dialog-title>
      {{ (isEdit ? 'orgUnit.dialog.editTitle' : 'orgUnit.dialog.newTitle') | t }}
    </h2>
    <mat-dialog-content>
      <form [formGroup]="form" class="form">
        <mat-form-field appearance="outline" class="full">
          <mat-label>{{ 'orgUnit.field.name' | t }}</mat-label>
          <input matInput formControlName="name" autocomplete="off" />
          @if (form.controls.name.hasError('required') && form.controls.name.touched) {
            <mat-error>{{ 'orgUnit.error.nameRequired' | t }}</mat-error>
          }
        </mat-form-field>

        <mat-form-field appearance="outline" class="full">
          <mat-label>{{ 'orgUnit.field.code' | t }}</mat-label>
          <input matInput formControlName="code" autocomplete="off" />
          @if (form.controls.code.hasError('required') && form.controls.code.touched) {
            <mat-error>{{ 'orgUnit.error.codeRequired' | t }}</mat-error>
          }
          @if (isEdit) {
            <mat-hint>{{ 'orgUnit.hint.codeImmutable' | t }}</mat-hint>
          }
        </mat-form-field>

        <app-searchable-select
          class="full"
          formControlName="parentOrgUnitId"
          [label]="'orgUnit.field.parent' | t"
          [options]="data.parentOptions"
          [clearable]="true"
          [clearLabel]="'orgUnit.value.topLevel' | t"
        />
      </form>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button matButton type="button" (click)="cancel()">{{ 'orgUnit.actions.cancel' | t }}</button>
      <button
        matButton="filled"
        type="button"
        (click)="submit()"
        [disabled]="form.invalid"
      >
        {{ (isEdit ? 'orgUnit.actions.save' : 'orgUnit.actions.create') | t }}
      </button>
    </mat-dialog-actions>
  `,
  styles: `
    .form {
      display: flex;
      flex-direction: column;
      min-width: 360px;
      gap: 0.25rem;
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
export class OrgUnitDialogComponent {
  readonly data = inject<OrgUnitDialogData>(MAT_DIALOG_DATA);
  private readonly dialogRef =
    inject<MatDialogRef<OrgUnitDialogComponent, OrgUnitDialogResult>>(MatDialogRef);
  private readonly fb = inject(FormBuilder);

  readonly isEdit = !!this.data.unit;

  readonly form = this.fb.nonNullable.group({
    name: [
      this.data.unit?.name ?? '',
      [Validators.required, Validators.maxLength(200)],
    ],
    code: [
      { value: this.data.unit?.code ?? '', disabled: this.isEdit },
      [Validators.required, Validators.maxLength(50)],
    ],
    parentOrgUnitId: [this.data.unit?.parentOrgUnitId ?? null as string | null],
  });

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const v = this.form.getRawValue();
    const parentOrgUnitId = v.parentOrgUnitId || null;

    if (this.isEdit && this.data.unit) {
      this.dialogRef.close({
        mode: 'edit',
        id: this.data.unit.id,
        name: v.name.trim(),
        parentOrgUnitId,
        original: {
          name: this.data.unit.name,
          parentOrgUnitId: this.data.unit.parentOrgUnitId,
        },
      });
    } else {
      this.dialogRef.close({
        mode: 'create',
        body: {
          name: v.name.trim(),
          code: v.code.trim(),
          parentOrgUnitId,
        },
      });
    }
  }

  cancel(): void {
    this.dialogRef.close();
  }
}
