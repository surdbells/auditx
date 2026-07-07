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
  CreateReferenceDataItemRequest,
  ReferenceDataItem,
  UpdateReferenceDataItemRequest,
} from '../../../../core/models';

export interface ReferenceDataDialogData {
  /** Human label for the category being edited (e.g. "Audit types"). */
  categoryLabel: string;
  /** Present when editing; absent for create. */
  item?: ReferenceDataItem;
}

export type ReferenceDataDialogResult =
  | { mode: 'create'; body: CreateReferenceDataItemRequest }
  | { mode: 'edit'; id: string; body: UpdateReferenceDataItemRequest };

@Component({
  selector: 'app-reference-data-dialog',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
  ],
  template: `
    <h2 mat-dialog-title>
      {{ isEdit ? 'Edit item' : 'New item' }}
    </h2>
    <mat-dialog-content>
      <form [formGroup]="form" class="form">
        <mat-form-field appearance="outline" class="full">
          <mat-label>Code</mat-label>
          <input matInput formControlName="code" autocomplete="off" />
          @if (isEdit) {
            <mat-hint>Code cannot be changed after creation.</mat-hint>
          }
          @if (form.controls.code.hasError('required') && form.controls.code.touched) {
            <mat-error>A code is required.</mat-error>
          }
        </mat-form-field>

        <mat-form-field appearance="outline" class="full">
          <mat-label>Label</mat-label>
          <input matInput formControlName="label" autocomplete="off" />
          @if (form.controls.label.hasError('required') && form.controls.label.touched) {
            <mat-error>A label is required.</mat-error>
          }
        </mat-form-field>

        <mat-form-field appearance="outline" class="full">
          <mat-label>Description</mat-label>
          <textarea matInput formControlName="description" rows="2"></textarea>
        </mat-form-field>

        <mat-form-field appearance="outline">
          <mat-label>Sort order</mat-label>
          <input matInput type="number" formControlName="sortOrder" min="0" />
        </mat-form-field>
      </form>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button matButton type="button" (click)="cancel()">Cancel</button>
      <button
        matButton="filled"
        type="button"
        (click)="submit()"
        [disabled]="form.invalid"
      >
        {{ isEdit ? 'Save' : 'Create' }}
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
export class ReferenceDataDialogComponent {
  readonly data = inject<ReferenceDataDialogData>(MAT_DIALOG_DATA);
  private readonly dialogRef =
    inject<
      MatDialogRef<ReferenceDataDialogComponent, ReferenceDataDialogResult>
    >(MatDialogRef);
  private readonly fb = inject(FormBuilder);

  readonly isEdit = !!this.data.item;

  readonly form = this.fb.nonNullable.group({
    code: [
      { value: this.data.item?.code ?? '', disabled: this.isEdit },
      [Validators.required, Validators.maxLength(120)],
    ],
    label: [
      this.data.item?.label ?? '',
      [Validators.required, Validators.maxLength(200)],
    ],
    description: [this.data.item?.description ?? ''],
    sortOrder: [this.data.item?.sortOrder ?? 0, [Validators.required]],
  });

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const v = this.form.getRawValue();
    const description = v.description.trim() || undefined;
    if (this.isEdit && this.data.item) {
      this.dialogRef.close({
        mode: 'edit',
        id: this.data.item.id,
        body: {
          label: v.label.trim(),
          description,
          sortOrder: Number(v.sortOrder),
        },
      });
    } else {
      this.dialogRef.close({
        mode: 'create',
        body: {
          code: v.code.trim(),
          label: v.label.trim(),
          description,
          sortOrder: Number(v.sortOrder),
        },
      });
    }
  }

  cancel(): void {
    this.dialogRef.close();
  }
}
