import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { AbstractControl, FormBuilder, ReactiveFormsModule, ValidationErrors, Validators } from '@angular/forms';
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
  /** The other items currently listed, so a sort position already in use is caught before saving. */
  siblings?: ReferenceDataItem[];
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
          @if (isEdit) {
            <!-- Renames are prospective: existing records keep the label they were created with. -->
            <mat-hint>A new label applies from now on; existing records keep the label they were created with.</mat-hint>
          }
          @if (form.controls.label.hasError('required') && form.controls.label.touched) {
            <mat-error>A label is required.</mat-error>
          }
        </mat-form-field>
        @if (history().length > 1) {
          <p class="history">
            Previously:
            @for (v of history().slice(0, -1); track v.effectiveFrom; let last = $last) {
              <span>“{{ v.label }}”</span>@if (!last) {, }
            }
          </p>
        }

        <mat-form-field appearance="outline" class="full">
          <mat-label>Description</mat-label>
          <textarea matInput formControlName="description" rows="2"></textarea>
        </mat-form-field>

        <mat-form-field appearance="outline">
          <mat-label>Sort order</mat-label>
          <input matInput type="number" formControlName="sortOrder" min="0" />
          @if (form.controls.sortOrder.hasError('taken')) {
            <mat-error>Position {{ form.controls.sortOrder.value }} is already used by “{{ form.controls.sortOrder.getError('taken') }}”.</mat-error>
          } @else if (form.controls.sortOrder.hasError('min')) {
            <mat-error>The position cannot be negative.</mat-error>
          } @else {
            <mat-hint>Each position is used once; the list is shown in this order.</mat-hint>
          }
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
    .history {
      margin: -0.25rem 0 0.5rem;
      font: var(--mat-sys-body-small);
      color: var(--mat-sys-on-surface-variant);
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

  /** Labels the item has carried, oldest first (empty for a new item or one never renamed). */
  history(): { label: string; effectiveFrom: string }[] {
    return this.data.item?.labelHistory ?? [];
  }

  /** The next free position — a new item lands at the end of the list instead of colliding at 0. */
  private readonly suggestedSortOrder =
    Math.max(-1, ...(this.data.siblings ?? []).map((s) => s.sortOrder)) + 1;

  /** Refuses a position another listed item already holds (the server checks every item, listed or not). */
  private readonly uniqueSortOrder = (control: AbstractControl<number>): ValidationErrors | null => {
    const value = Number(control.value);
    // Like the server: an item keeping the position it already has is fine (older data may share one); only a
    // position being newly chosen must be free.
    if (this.data.item && value === this.data.item.sortOrder) {
      return null;
    }
    const taken = (this.data.siblings ?? []).find(
      (s) => s.sortOrder === value && s.id !== this.data.item?.id,
    );
    return taken ? { taken: taken.label } : null;
  };

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
    sortOrder: [
      this.data.item?.sortOrder ?? this.suggestedSortOrder,
      [Validators.required, Validators.min(0), this.uniqueSortOrder],
    ],
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
