import { ChangeDetectionStrategy, Component, Signal, inject } from '@angular/core';
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

import { CreateAcActionItemRequest, UserDirectoryEntry } from '../../../core/models';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';

export interface CreateActionItemDialogData {
  /**
   * User directory for the optional assignee picker (a signal so the options fill in even if the lazy
   * directory load completes after the dialog opens). Sourced from the authenticated-only /users/directory,
   * which every AC member can read — unlike the ManageUsers-gated admin user list.
   */
  users: Signal<UserDirectoryEntry[]>;
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
    TranslatePipe,
  ],
  template: `
    <h2 mat-dialog-title>{{ 'ac.items.new' | t }}</h2>
    <mat-dialog-content>
      <form [formGroup]="form" class="form">
        <mat-form-field appearance="outline" class="full">
          <mat-label>{{ 'ac.createItem.title' | t }}</mat-label>
          <input matInput formControlName="title" maxlength="200" />
        </mat-form-field>
        <mat-form-field appearance="outline" class="full">
          <mat-label>{{ 'ac.createItem.description' | t }}</mat-label>
          <textarea matInput formControlName="description" rows="3"></textarea>
        </mat-form-field>
        <mat-form-field appearance="outline" class="full">
          <mat-label>{{ 'ac.createItem.assignTo' | t }}</mat-label>
          <mat-select formControlName="assignedToUserId">
            <mat-option [value]="null">{{ 'ac.createItem.unassigned' | t }}</mat-option>
            @for (u of data.users(); track u.id) {
              <mat-option [value]="u.id">
                {{ u.displayName }}
              </mat-option>
            }
          </mat-select>
        </mat-form-field>
        <mat-form-field appearance="outline" class="full">
          <mat-label>{{ 'ac.createItem.dueDate' | t }}</mat-label>
          <input matInput type="date" formControlName="dueDate" />
        </mat-form-field>
      </form>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button matButton type="button" (click)="cancel()">{{ 'ac.common.cancel' | t }}</button>
      <button
        matButton="filled"
        type="button"
        (click)="submit()"
        [disabled]="form.invalid"
      >
        {{ 'ac.common.create' | t }}
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