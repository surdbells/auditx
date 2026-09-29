import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { AbstractControl, FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import {
  MAT_DIALOG_DATA,
  MatDialogModule,
  MatDialogRef,
} from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';

import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import {
  CreateLocalUserRequest,
  CreateUserRequest,
  InitialPasswordMethod,
  RoleDto,
} from '../../../../core/models';

export interface CreateUserDialogData {
  roles: RoleDto[];
  /** Whether local passwords are enabled for the institution (offers the local option when true). */
  localPasswordsEnabled: boolean;
}

/** Discriminated result: a directory user, or a local-password user. */
export type CreateUserDialogResult =
  | { kind: 'directory'; request: CreateUserRequest }
  | { kind: 'local'; request: CreateLocalUserRequest };

/** Create a user — a directory (AD) account, or a local-password account (email, name, username, method). */
@Component({
  selector: 'app-create-user-dialog',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatButtonModule,
    MatButtonToggleModule,
    TranslatePipe,
  ],
  template: `
    <h2 mat-dialog-title>{{ 'identity.createUser.title' | t }}</h2>
    <mat-dialog-content>
      <form [formGroup]="form" class="form">
        @if (data.localPasswordsEnabled) {
          <mat-button-toggle-group formControlName="authType" class="auth-type" aria-label="Authentication type">
            <mat-button-toggle value="directory">{{ 'identity.createUser.directory' | t }}</mat-button-toggle>
            <mat-button-toggle value="local">{{ 'identity.createUser.local' | t }}</mat-button-toggle>
          </mat-button-toggle-group>
        }

        <mat-form-field appearance="outline" class="full">
          <mat-label>{{ 'identity.field.email' | t }}</mat-label>
          <input matInput type="email" formControlName="email" autocomplete="off" />
          @if (form.controls.email.hasError('required') && form.controls.email.touched) {
            <mat-error>{{ 'identity.createUser.emailRequired' | t }}</mat-error>
          }
          @if (form.controls.email.hasError('email') && form.controls.email.touched) {
            <mat-error>{{ 'identity.createUser.emailInvalid' | t }}</mat-error>
          }
        </mat-form-field>
        <div class="row">
          <mat-form-field appearance="outline">
            <mat-label>{{ 'identity.field.firstName' | t }}</mat-label>
            <input matInput formControlName="firstName" autocomplete="off" />
          </mat-form-field>
          <mat-form-field appearance="outline">
            <mat-label>{{ 'identity.field.lastName' | t }}</mat-label>
            <input matInput formControlName="lastName" autocomplete="off" />
          </mat-form-field>
        </div>

        @if (isLocal()) {
          <mat-form-field appearance="outline" class="full">
            <mat-label>{{ 'identity.createUser.username' | t }}</mat-label>
            <input matInput formControlName="username" autocomplete="off" />
            @if (form.controls.username.hasError('required') && form.controls.username.touched) {
              <mat-error>{{ 'identity.createUser.usernameRequired' | t }}</mat-error>
            }
          </mat-form-field>
          <mat-form-field appearance="outline" class="full">
            <mat-label>{{ 'identity.createUser.method' | t }}</mat-label>
            <mat-select formControlName="method">
              <mat-option value="set_password">{{ 'identity.createUser.method.set' | t }}</mat-option>
              <mat-option value="generate_temp">{{ 'identity.createUser.method.generate' | t }}</mat-option>
              <mat-option value="invite">{{ 'identity.createUser.method.invite' | t }}</mat-option>
            </mat-select>
          </mat-form-field>
          @if (form.controls.method.value === 'set_password') {
            <mat-form-field appearance="outline" class="full">
              <mat-label>{{ 'identity.createUser.password' | t }}</mat-label>
              <input matInput type="password" formControlName="password" autocomplete="new-password" />
              @if (form.controls.password.hasError('required') && form.controls.password.touched) {
                <mat-error>{{ 'identity.createUser.passwordRequired' | t }}</mat-error>
              }
            </mat-form-field>
          }
        }

        <mat-form-field appearance="outline" class="full">
          <mat-label>{{ 'identity.createUser.roles' | t }}</mat-label>
          <mat-select formControlName="roleNames" multiple>
            @for (role of data.roles; track role.id) {
              <mat-option [value]="role.name">{{ role.name }}</mat-option>
            }
          </mat-select>
          <mat-hint>{{ 'identity.createUser.rolesHint' | t }}</mat-hint>
        </mat-form-field>
      </form>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button matButton type="button" (click)="cancel()">{{ 'identity.actions.cancel' | t }}</button>
      <button matButton="filled" type="button" (click)="submit()" [disabled]="form.invalid">
        {{ 'identity.createUser.submit' | t }}
      </button>
    </mat-dialog-actions>
  `,
  styles: `
    .form { display: flex; flex-direction: column; min-width: 380px; }
    .full { width: 100%; }
    .auth-type { margin-bottom: 12px; }
    .row { display: flex; gap: 1rem; flex-wrap: wrap; }
    .row mat-form-field { flex: 1 1 150px; }
    @media (max-width: 480px) { .form { min-width: auto; } }
  `,
})
export class CreateUserDialogComponent {
  readonly data = inject<CreateUserDialogData>(MAT_DIALOG_DATA);
  private readonly dialogRef =
    inject<MatDialogRef<CreateUserDialogComponent, CreateUserDialogResult>>(MatDialogRef);
  private readonly fb = inject(FormBuilder);

  readonly form = this.fb.nonNullable.group({
    authType: ['directory' as 'directory' | 'local'],
    email: ['', [Validators.required, Validators.email]],
    firstName: [''],
    lastName: [''],
    username: [''],
    method: ['set_password' as InitialPasswordMethod],
    password: [''],
    roleNames: [[] as string[]],
  });

  readonly isLocal = signal(false);

  constructor() {
    // Toggle validators on the local-only controls as the auth type / method changes.
    this.form.controls.authType.valueChanges.subscribe((type) => {
      const local = type === 'local';
      this.isLocal.set(local);
      this.setRequired(this.form.controls.username, local);
      this.applyPasswordValidator();
    });
    this.form.controls.method.valueChanges.subscribe(() => this.applyPasswordValidator());
  }

  private applyPasswordValidator(): void {
    const need = this.isLocal() && this.form.controls.method.value === 'set_password';
    this.setRequired(this.form.controls.password, need);
  }

  private setRequired(control: AbstractControl, required: boolean): void {
    control.setValidators(required ? [Validators.required] : []);
    control.updateValueAndValidity();
  }

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const v = this.form.getRawValue();
    if (v.authType === 'local') {
      this.dialogRef.close({
        kind: 'local',
        request: {
          email: v.email.trim(),
          firstName: v.firstName.trim(),
          lastName: v.lastName.trim(),
          username: v.username.trim(),
          roleNames: v.roleNames,
          method: v.method,
          password: v.method === 'set_password' ? v.password : null,
        },
      });
      return;
    }
    this.dialogRef.close({
      kind: 'directory',
      request: {
        email: v.email.trim(),
        firstName: v.firstName.trim(),
        lastName: v.lastName.trim(),
        externalId: null,
        roleNames: v.roleNames,
      },
    });
  }

  cancel(): void {
    this.dialogRef.close();
  }
}
