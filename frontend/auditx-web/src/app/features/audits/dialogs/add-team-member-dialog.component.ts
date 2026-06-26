import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import {
  MAT_DIALOG_DATA,
  MatDialogModule,
  MatDialogRef,
} from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatSelectModule } from '@angular/material/select';

import { TeamRole, UserDto } from '../../../core/models';

export interface AddTeamMemberDialogData {
  /** Users selectable as the new member. */
  users: UserDto[];
}

export interface AddTeamMemberDialogResult {
  userId: string;
  teamRole: TeamRole;
}

@Component({
  selector: 'app-add-team-member-dialog',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    MatDialogModule,
    MatFormFieldModule,
    MatSelectModule,
    MatButtonModule,
  ],
  template: `
    <h2 mat-dialog-title>Add team member</h2>
    <mat-dialog-content>
      <form [formGroup]="form" class="form">
        <mat-form-field appearance="outline" class="full">
          <mat-label>User</mat-label>
          <mat-select formControlName="userId">
            @for (u of data.users; track u.id) {
              <mat-option [value]="u.id">{{ u.displayName }}</mat-option>
            }
          </mat-select>
          @if (form.controls.userId.hasError('required') && form.controls.userId.touched) {
            <mat-error>Select a user.</mat-error>
          }
        </mat-form-field>

        <mat-form-field appearance="outline" class="full">
          <mat-label>Role</mat-label>
          <mat-select formControlName="teamRole">
            <mat-option value="auditor">Auditor</mat-option>
            <mat-option value="reviewer">Reviewer</mat-option>
            <mat-option value="auditee">Auditee</mat-option>
            <mat-option value="lead">Lead</mat-option>
          </mat-select>
        </mat-form-field>
      </form>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button matButton type="button" (click)="cancel()">Cancel</button>
      <button matButton="filled" type="button" (click)="submit()" [disabled]="form.invalid">
        Add member
      </button>
    </mat-dialog-actions>
  `,
  styles: `
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
export class AddTeamMemberDialogComponent {
  readonly data = inject<AddTeamMemberDialogData>(MAT_DIALOG_DATA);
  private readonly dialogRef = inject<
    MatDialogRef<AddTeamMemberDialogComponent, AddTeamMemberDialogResult>
  >(MatDialogRef);
  private readonly fb = inject(FormBuilder);

  readonly form = this.fb.nonNullable.group({
    userId: ['', [Validators.required]],
    teamRole: ['auditor' as TeamRole, [Validators.required]],
  });

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const v = this.form.getRawValue();
    this.dialogRef.close({ userId: v.userId, teamRole: v.teamRole });
  }

  cancel(): void {
    this.dialogRef.close();
  }
}
