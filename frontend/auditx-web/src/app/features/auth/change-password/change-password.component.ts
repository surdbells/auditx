import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';

import { AuthService } from '../../../core/services/auth.service';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';

/** Change the signed-in user's local password. Doubles as the forced first-login / expiry change screen. */
@Component({
  selector: 'app-change-password',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    MatProgressBarModule,
    TranslatePipe,
  ],
  template: `
    <div class="auth-page">
      <mat-card class="auth-card">
        @if (submitting()) {
          <mat-progress-bar mode="indeterminate" />
        }
        <mat-card-header>
          <mat-card-title>{{ 'auth.changePassword.title' | t }}</mat-card-title>
        </mat-card-header>
        <mat-card-content>
          @if (auth.mustChangePassword()) {
            <p class="auth-note" role="status">{{ 'auth.changePassword.mustChange' | t }}</p>
          }
          @if (errorMessage(); as msg) {
            <p class="auth-error" role="alert">{{ msg | t }}</p>
          }
          <form [formGroup]="form" (ngSubmit)="submit()" class="auth-form">
            <mat-form-field appearance="outline">
              <mat-label>{{ 'auth.changePassword.current' | t }}</mat-label>
              <input matInput type="password" formControlName="currentPassword" autocomplete="current-password" />
            </mat-form-field>
            <mat-form-field appearance="outline">
              <mat-label>{{ 'auth.changePassword.new' | t }}</mat-label>
              <input matInput type="password" formControlName="newPassword" autocomplete="new-password" />
            </mat-form-field>
            <mat-form-field appearance="outline">
              <mat-label>{{ 'auth.changePassword.confirm' | t }}</mat-label>
              <input matInput type="password" formControlName="confirmPassword" autocomplete="new-password" />
              @if (form.hasError('mismatch') && form.controls.confirmPassword.touched) {
                <mat-error>{{ 'auth.changePassword.mismatch' | t }}</mat-error>
              }
            </mat-form-field>
            <button matButton="filled" type="submit" [disabled]="submitting()">
              {{ (submitting() ? 'common.saving' : 'auth.changePassword.submit') | t }}
            </button>
          </form>
        </mat-card-content>
      </mat-card>
    </div>
  `,
  styles: `
    .auth-page { min-height: 100dvh; display: grid; place-items: center; padding: 24px 16px; }
    .auth-card { width: 100%; max-width: 420px; }
    .auth-form { display: flex; flex-direction: column; gap: 4px; margin-top: 8px; }
    .auth-note { color: var(--mat-sys-primary, #4f46e5); margin: 0 0 12px; }
    .auth-error { color: var(--mat-sys-error, #b3261e); margin: 0 0 12px; }
  `,
})
export class ChangePasswordComponent {
  private readonly fb = inject(FormBuilder);
  readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  readonly submitting = signal(false);
  readonly errorMessage = signal<string | null>(null);

  readonly form = this.fb.nonNullable.group(
    {
      currentPassword: ['', [Validators.required]],
      newPassword: ['', [Validators.required]],
      confirmPassword: ['', [Validators.required]],
    },
    { validators: (group) => (group.get('newPassword')?.value === group.get('confirmPassword')?.value ? null : { mismatch: true }) },
  );

  submit(): void {
    if (this.form.invalid || this.submitting()) {
      this.form.markAllAsTouched();
      return;
    }
    this.errorMessage.set(null);
    this.submitting.set(true);
    const { currentPassword, newPassword } = this.form.getRawValue();

    this.auth.changePassword({ currentPassword, newPassword }).subscribe({
      next: () => {
        this.submitting.set(false);
        void this.router.navigateByUrl('/dashboard');
      },
      error: (err: unknown) => {
        this.submitting.set(false);
        const failures = (err as { error?: { field_errors?: { field: string; message: string }[] } })?.error?.field_errors;
        this.errorMessage.set(
          failures?.some((f) => f.field === 'currentPassword')
            ? 'auth.changePassword.currentIncorrect'
            : 'auth.changePassword.policyError',
        );
      },
    });
  }
}
