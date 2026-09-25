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

import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { UpdateUserProfileRequest } from '../../../../core/models';

export interface EditUserDialogData {
  email: string;
  firstName: string;
  lastName: string;
  displayName: string;
}

/** Edit a user's profile fields (name, email, display name). */
@Component({
  selector: 'app-edit-user-dialog',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    TranslatePipe,
  ],
  template: `
    <h2 mat-dialog-title>{{ 'identity.editUser.title' | t }}</h2>
    <mat-dialog-content>
      <form [formGroup]="form" class="form">
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
        <mat-form-field appearance="outline" class="full">
          <mat-label>{{ 'identity.editUser.displayName' | t }}</mat-label>
          <input matInput formControlName="displayName" autocomplete="off" />
          <mat-hint>{{ 'identity.editUser.displayNameHint' | t }}</mat-hint>
        </mat-form-field>
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
      </form>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button matButton type="button" (click)="cancel()">{{ 'identity.actions.cancel' | t }}</button>
      <button matButton="filled" type="button" (click)="submit()" [disabled]="form.invalid">
        {{ 'identity.editUser.submit' | t }}
      </button>
    </mat-dialog-actions>
  `,
  styles: `
    .form {
      display: flex;
      flex-direction: column;
      min-width: 380px;
    }
    .full {
      width: 100%;
    }
    .row {
      display: flex;
      gap: 1rem;
      flex-wrap: wrap;
    }
    .row mat-form-field {
      flex: 1 1 150px;
    }
    @media (max-width: 480px) {
      .form {
        min-width: auto;
      }
    }
  `,
})
export class EditUserDialogComponent {
  readonly data = inject<EditUserDialogData>(MAT_DIALOG_DATA);
  private readonly dialogRef =
    inject<MatDialogRef<EditUserDialogComponent, UpdateUserProfileRequest>>(MatDialogRef);
  private readonly fb = inject(FormBuilder);

  readonly form = this.fb.nonNullable.group({
    firstName: [this.data.firstName],
    lastName: [this.data.lastName],
    displayName: [this.data.displayName],
    email: [this.data.email, [Validators.required, Validators.email]],
  });

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const v = this.form.getRawValue();
    this.dialogRef.close({
      email: v.email.trim(),
      firstName: v.firstName.trim(),
      lastName: v.lastName.trim(),
      displayName: v.displayName.trim() || null,
    });
  }

  cancel(): void {
    this.dialogRef.close();
  }
}
