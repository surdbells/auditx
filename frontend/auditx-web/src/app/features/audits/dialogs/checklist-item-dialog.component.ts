import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import {
  MAT_DIALOG_DATA,
  MatDialogModule,
  MatDialogRef,
} from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';

import {
  AuditChecklistItem,
  ResponseType,
  UserDto,
} from '../../../core/models';

export interface ChecklistItemDialogData {
  /** Present when editing an existing item. */
  item?: AuditChecklistItem;
  /** Users selectable as the assignee. */
  users: UserDto[];
}

export interface ChecklistItemDialogResult {
  prompt: string;
  referenceNotes: string | null;
  sectionName: string | null;
  responseType: ResponseType;
  isRequired: boolean;
  assignedUserId: string | null;
}

@Component({
  selector: 'app-checklist-item-dialog',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatCheckboxModule,
    MatButtonModule,
  ],
  template: `
    <h2 mat-dialog-title>{{ isEdit ? 'Edit checklist item' : 'Add checklist item' }}</h2>
    <mat-dialog-content>
      <form [formGroup]="form" class="form">
        <mat-form-field appearance="outline" class="full">
          <mat-label>Prompt</mat-label>
          <textarea matInput formControlName="prompt" rows="2"></textarea>
          @if (form.controls.prompt.hasError('required') && form.controls.prompt.touched) {
            <mat-error>A prompt is required.</mat-error>
          }
        </mat-form-field>

        <mat-form-field appearance="outline" class="full">
          <mat-label>Reference notes</mat-label>
          <textarea matInput formControlName="referenceNotes" rows="2"></textarea>
        </mat-form-field>

        <div class="row">
          <mat-form-field appearance="outline">
            <mat-label>Section</mat-label>
            <input matInput formControlName="sectionName" autocomplete="off" />
          </mat-form-field>
          <mat-form-field appearance="outline">
            <mat-label>Response type</mat-label>
            <mat-select formControlName="responseType">
              <mat-option value="pass_fail_na">Pass / Fail / N/A</mat-option>
            </mat-select>
          </mat-form-field>
        </div>

        <mat-form-field appearance="outline" class="full">
          <mat-label>Assignee</mat-label>
          <mat-select formControlName="assignedUserId">
            <mat-option [value]="''">Unassigned</mat-option>
            @for (u of data.users; track u.id) {
              <mat-option [value]="u.id">{{ u.displayName }}</mat-option>
            }
          </mat-select>
        </mat-form-field>

        <mat-checkbox formControlName="isRequired">Required</mat-checkbox>
      </form>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button matButton type="button" (click)="cancel()">Cancel</button>
      <button matButton="filled" type="button" (click)="submit()" [disabled]="form.invalid">
        {{ isEdit ? 'Save' : 'Add item' }}
      </button>
    </mat-dialog-actions>
  `,
  styles: `
    .form {
      display: flex;
      flex-direction: column;
      min-width: 460px;
      gap: 0.25rem;
    }
    .row {
      display: flex;
      gap: 1rem;
    }
    .row mat-form-field {
      flex: 1;
    }
    .full {
      width: 100%;
    }
    @media (max-width: 560px) {
      .form {
        min-width: auto;
      }
      .row {
        flex-direction: column;
      }
    }
  `,
})
export class ChecklistItemDialogComponent {
  readonly data = inject<ChecklistItemDialogData>(MAT_DIALOG_DATA);
  private readonly dialogRef = inject<
    MatDialogRef<ChecklistItemDialogComponent, ChecklistItemDialogResult>
  >(MatDialogRef);
  private readonly fb = inject(FormBuilder);

  readonly isEdit = !!this.data.item;

  readonly form = this.fb.nonNullable.group({
    prompt: [this.data.item?.prompt ?? '', [Validators.required]],
    referenceNotes: [this.data.item?.referenceNotes ?? ''],
    sectionName: [this.data.item?.sectionName ?? ''],
    responseType: [
      (this.data.item?.responseType ?? 'pass_fail_na') as ResponseType,
      [Validators.required],
    ],
    assignedUserId: [this.data.item?.assignedUserId ?? ''],
    isRequired: [this.data.item?.isRequired ?? true],
  });

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const v = this.form.getRawValue();
    this.dialogRef.close({
      prompt: v.prompt.trim(),
      referenceNotes: v.referenceNotes.trim() || null,
      sectionName: v.sectionName.trim() || null,
      responseType: v.responseType,
      isRequired: v.isRequired,
      assignedUserId: v.assignedUserId || null,
    });
  }

  cancel(): void {
    this.dialogRef.close();
  }
}
