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

import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { PasswordFieldComponent } from '../../../../shared/components/password-field/password-field.component';
import { AdminResetPasswordRequest, InitialPasswordMethod, PasswordPolicy } from '../../../../core/models';

export interface ResetPasswordDialogData {
  displayName: string;
  /** When true the user is not yet local, so a username is also required (enable-local flow). */
  requireUsername: boolean;
  /** Institution password policy, so the set-password field can validate inline (falls back to defaults). */
  passwordPolicy?: PasswordPolicy | null;
}

export type ResetPasswordDialogResult = AdminResetPasswordRequest & { username?: string };

/** Admin: set/generate/invite a local password for a user (reset, or enable-local when a username is required). */
@Component({
  selector: 'app-reset-password-dialog',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatButtonModule,
    TranslatePipe,
    PasswordFieldComponent,
  ],
  template: `
    <h2 mat-dialog-title>{{ 'identity.resetPassword.title' | t }}</h2>
    <mat-dialog-content>
      <p>{{ 'identity.resetPassword.for' | t: { name: data.displayName } }}</p>
      <form [formGroup]="form" class="form">
        @if (data.requireUsername) {
          <mat-form-field appearance="outline" class="full">
            <mat-label>{{ 'identity.createUser.username' | t }}</mat-label>
            <input matInput formControlName="username" autocomplete="off" />
          </mat-form-field>
        }
        <mat-form-field appearance="outline" class="full">
          <mat-label>{{ 'identity.createUser.method' | t }}</mat-label>
          <mat-select formControlName="method">
            <mat-option value="set_password">{{ 'identity.createUser.method.set' | t }}</mat-option>
            <mat-option value="generate_temp">{{ 'identity.createUser.method.generate' | t }}</mat-option>
            <mat-option value="invite">{{ 'identity.createUser.method.invite' | t }}</mat-option>
          </mat-select>
        </mat-form-field>
        @if (form.controls.method.value === 'set_password') {
          <app-password-field
            class="full"
            formControlName="password"
            [policy]="data.passwordPolicy ?? null"
          />
        }
      </form>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button matButton type="button" (click)="cancel()">{{ 'identity.actions.cancel' | t }}</button>
      <button matButton="filled" type="button" (click)="submit()">{{ 'identity.resetPassword.submit' | t }}</button>
    </mat-dialog-actions>
  `,
  styles: `.form { display: flex; flex-direction: column; min-width: 360px; } .full { width: 100%; }`,
})
export class ResetPasswordDialogComponent {
  readonly data = inject<ResetPasswordDialogData>(MAT_DIALOG_DATA);
  private readonly dialogRef = inject<MatDialogRef<ResetPasswordDialogComponent, ResetPasswordDialogResult>>(MatDialogRef);
  private readonly fb = inject(FormBuilder);

  readonly form = this.fb.nonNullable.group({
    username: [''],
    method: ['set_password' as InitialPasswordMethod],
    password: [''],
  });

  submit(): void {
    const v = this.form.getRawValue();
    if (this.data.requireUsername && !v.username.trim()) {
      this.form.controls.username.setValidators([Validators.required]);
      this.form.controls.username.markAsTouched();
      this.form.controls.username.updateValueAndValidity();
      return;
    }
    if (v.method === 'set_password' && this.form.controls.password.invalid) {
      this.form.controls.password.markAsTouched();
      return;
    }
    this.dialogRef.close({
      method: v.method,
      password: v.method === 'set_password' ? v.password : null,
      username: this.data.requireUsername ? v.username.trim() : undefined,
    });
  }

  cancel(): void {
    this.dialogRef.close();
  }
}
