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

import { CreateAcActionItemRequest, UserDto } from '../../../core/models';

export interface CreateActionItemDialogData {
  /** Active users for the optional assignee picker. */
  users: UserDto[];
}

/** Create an AC action item. Title is required; assignee + due date optional. */
@Component({
  selector: 'app-create-action-item-dialog',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatButtonModule,
  ],
  template: `
    <h2 mat-dialog-title>New action item</h2>
    <mat-dialog-content>
      <form [formGroup]="form" class="form">
        <mat-form-field appearance="outline" class="full">
          <mat-label>Title</mat-label>
          <input matInput formControlName="title" maxlength="200" />
        </mat-form-field>
        <mat-form-field appearance="outline" class="full">
          <mat-label>Description (optional)</mat-label>
          <textarea matInput formControlName="description" rows="3"></textarea>
        </mat-form-field>
        <mat-form-field appearance="outline" class="full">
          <mat-label>Assign to (optional)</mat-label>
          <mat-select formControlName="assignedToUserId">
            <mat-option [value]="null">Unassigned</mat-option>
            @for (u of data.users; track u.id) {
              <mat-option [value]="u.id">
                {{ u.displayName }} ({{ u.email }})
              </mat-option>
            }
          </mat-select>
        </mat-form-field>
        <mat-form-field appearance="outline" class="full">
          <mat-label>Due date (optional)</mat-label>
          <input matInput type="date" formControlName="dueDate" />
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
        Create
      </button>
    </mat-dialog-actions>
  `,
  styles: `
    .form {
      display: flex;
      flex-direction: column;
      min-width: 440px;
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
export class CreateActionItemDialogComponent {
  readonly data = inject<CreateActionItemDialogData>(MAT_DIALOG_DATA);
  private readonly dialogRef =
    inject<
      MatDialogRef<CreateActionItemDialogComponent, CreateAcActionItemRequest>
    >(MatDialogRef);
  private readonly fb = inject(FormBuilder);

  readonly form = this.fb.nonNullable.group({
    title: ['', [Validators.required, Validators.maxLength(200)]],
    description: [''],
    assignedToUserId: [null as string | null],
    dueDate: [''],
  });

  submit(): void {
    if (this.form.invalid) {
      return;
    }
    const v = this.form.getRawValue();
    this.dialogRef.close({
      title: v.title.trim(),
      description: v.description.trim() || null,
      assignedToUserId: v.assignedToUserId || null,
      dueDate: v.dueDate || null,
    });
  }

  cancel(): void {
    this.dialogRef.close();
  }
}