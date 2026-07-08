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
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';

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
    TranslatePipe,
  ],
  template: `
    <h2 mat-dialog-title>{{ 'identity.grantRole.title' | t }}</h2>
    <mat-dialog-content>
      <p class="hint">
        {{ 'identity.grantRole.hintBefore' | t
        }}<strong>{{ data.userDisplayName }}</strong
        >{{ 'identity.grantRole.hintAfter' | t }}
      </p>
      <form [formGroup]="form" class="form">
        <mat-form-field appearance="outline" class="full">
          <mat-label>{{ 'identity.field.role' | t }}</mat-label>
          <mat-select formControlName="roleId">
            @for (role of data.roles; track role.id) {
              <mat-option [value]="role.id">{{ role.name }}</mat-option>
            }
          </mat-select>
          @if (form.controls.roleId.hasError('required') && form.controls.roleId.touched) {
            <mat-error>{{ 'identity.grantRole.roleRequired' | t }}</mat-error>
          }
        </mat-form-field>

        <mat-form-field appearance="outline" class="full">
          <mat-label>{{ 'identity.grantRole.scopeLabel' | t }}</mat-label>
          <input matInput formControlName="scopeValue" [placeholder]="'identity.grantRole.scopePlaceholder' | t" />
          <mat-hint>{{ 'identity.grantRole.scopeHint' | t }}</mat-hint>
        </mat-form-field>
      </form>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button matButton type="button" (click)="cancel()">{{ 'identity.actions.cancel' | t }}</button>
      <button matButton="filled" type="button" (click)="submit()" [disabled]="form.invalid">
        {{ 'identity.grantRole.title' | t }}
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
