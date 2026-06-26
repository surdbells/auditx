import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import {
  MAT_DIALOG_DATA,
  MatDialogModule,
  MatDialogRef,
} from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatSelectModule } from '@angular/material/select';

/** A team member selectable as an item assignee. */
export interface AssignableMember {
  userId: string;
  displayName: string;
  teamRole: string;
}

export interface AssignItemDialogData {
  /** The checklist item prompt, for context. */
  prompt: string;
  /** Currently assigned user, if any. */
  currentUserId?: string | null;
  /** Active team members eligible to take the item. */
  members: AssignableMember[];
}

export interface AssignItemDialogResult {
  /** `null` = unassign. */
  assignedUserId: string | null;
}

@Component({
  selector: 'app-assign-item-dialog',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    MatDialogModule,
    MatFormFieldModule,
    MatSelectModule,
    MatButtonModule,
  ],
  template: `
    <h2 mat-dialog-title>Assign item</h2>
    <mat-dialog-content>
      <p class="prompt">{{ data.prompt }}</p>
      <form [formGroup]="form" class="form">
        <mat-form-field appearance="outline" class="full">
          <mat-label>Assignee</mat-label>
          <mat-select formControlName="assignedUserId">
            <mat-option [value]="''">Unassigned</mat-option>
            @for (m of data.members; track m.userId) {
              <mat-option [value]="m.userId">
                {{ m.displayName }} ({{ m.teamRole }})
              </mat-option>
            }
          </mat-select>
        </mat-form-field>
      </form>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button matButton type="button" (click)="cancel()">Cancel</button>
      <button matButton="filled" type="button" (click)="submit()">Save</button>
    </mat-dialog-actions>
  `,
  styles: `
    .prompt {
      margin: 0 0 1rem;
      font-weight: 500;
    }
    .form {
      display: flex;
      flex-direction: column;
      min-width: 400px;
    }
    .full {
      width: 100%;
    }
    @media (max-width: 520px) {
      .form {
        min-width: auto;
      }
    }
  `,
})
export class AssignItemDialogComponent {
  readonly data = inject<AssignItemDialogData>(MAT_DIALOG_DATA);
  private readonly dialogRef = inject<
    MatDialogRef<AssignItemDialogComponent, AssignItemDialogResult>
  >(MatDialogRef);
  private readonly fb = inject(FormBuilder);

  readonly form = this.fb.nonNullable.group({
    assignedUserId: [this.data.currentUserId ?? ''],
  });

  submit(): void {
    this.dialogRef.close({
      assignedUserId: this.form.getRawValue().assignedUserId || null,
    });
  }

  cancel(): void {
    this.dialogRef.close();
  }
}
