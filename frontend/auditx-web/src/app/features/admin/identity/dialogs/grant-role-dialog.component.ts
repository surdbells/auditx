import {
  ChangeDetectionStrategy,
  Component,
  inject,
  signal,
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

import { GrantRoleRequest, RoleDto } from '../../../../core/models';

export interface GrantRoleDialogData {
  userDisplayName: string;
  roles: RoleDto[];
}

@Component({
  selector: 'app-grant-role-dialog',
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
    <h2 mat-dialog-title>Grant role</h2>
    <mat-dialog-content>
      <p class="hint">
        Assign a role to <strong>{{ data.userDisplayName }}</strong>. Optionally restrict
        it to a scope (e.g. a business unit or branch code).
      </p>
      <form [formGroup]="form" class="form">
        <mat-form-field appearance="outline" class="full">
          <mat-label>Role</mat-label>
          <mat-select formControlName="roleId">
            @for (role of data.roles; track role.id) {
              <mat-option [value]="role.id">{{ role.name }}</mat-option>
            }
          </mat-select>
          @if (form.controls.roleId.hasError('required') && form.controls.roleId.touched) {
            <mat-error>Please select a role.</mat-error>
          }
        </mat-form-field>

        <mat-form-field appearance="outline" class="full">
          <mat-label>Scope value (optional)</mat-label>
          <input matInput formControlName="scopeValue" placeholder="e.g. BU-RETAIL" />
          <mat-hint>Leave blank for an unscoped (global) grant.</mat-hint>
        </mat-form-field>
      </form>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button matButton type="button" (click)="cancel()">Cancel</button>
      <button matButton="filled" type="button" (click)="submit()" [disabled]="form.invalid">
        Grant role
      </button>
    </mat-dialog-actions>
  `,
  styles: `
    .form {
      display: flex;
      flex-direction: column;
      min-width: 360px;
    }
    .full {
      width: 100%;
    }
    .hint {
      color: var(--mat-sys-on-surface-variant);
      margin-top: 0;
    }
    @media (max-width: 480px) {
      .form {
        min-width: auto;
      }
    }
  `,
})
export class GrantRoleDialogComponent {
  readonly data = inject<GrantRoleDialogData>(MAT_DIALOG_DATA);
  private readonly dialogRef =
    inject<MatDialogRef<GrantRoleDialogComponent, GrantRoleRequest>>(
      MatDialogRef,
    );
  private readonly fb = inject(FormBuilder);

  readonly submitted = signal(false);

  readonly form = this.fb.nonNullable.group({
    roleId: ['', [Validators.required]],
    scopeValue: [''],
  });

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const { roleId, scopeValue } = this.form.getRawValue();
    const result: GrantRoleRequest = {
      roleId,
      scopeValue: scopeValue.trim() ? scopeValue.trim() : null,
    };
    this.dialogRef.close(result);
  }

  cancel(): void {
    this.dialogRef.close();
  }
}
