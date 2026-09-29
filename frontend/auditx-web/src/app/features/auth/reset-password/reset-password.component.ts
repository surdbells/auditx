import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';

import { AuthService } from '../../../core/services/auth.service';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';

/** Set a new password from an emailed reset/invite link (token in the query string). */
@Component({
  selector: 'app-reset-password',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    RouterLink,
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
          <mat-card-title>{{ 'auth.resetPassword.title' | t }}</mat-card-title>
        </mat-card-header>
        <mat-card-content>
          @if (done()) {
            <p class="auth-note" role="status">{{ 'auth.resetPassword.done' | t }}</p>
            <a routerLink="/login" matButton="filled">{{ 'auth.resetPassword.signIn' | t }}</a>
          } @else if (!token) {
            <p class="auth-error" role="alert">{{ 'auth.resetPassword.missingToken' | t }}</p>
            <a routerLink="/forgot-password" matButton>{{ 'auth.resetPassword.requestNew' | t }}</a>
          } @else {
            @if (errorMessage(); as msg) {
              <p class="auth-error" role="alert">{{ msg | t }}</p>
            }
            <form [formGroup]="form" (ngSubmit)="submit()" class="auth-form">
              <mat-form-field appearance="outline">
                <mat-label>{{ 'auth.resetPassword.new' | t }}</mat-label>
                <input matInput type="password" formControlName="newPassword" autocomplete="new-password" />
              </mat-form-field>
              <mat-form-field appearance="outline">
                <mat-label>{{ 'auth.resetPassword.confirm' | t }}</mat-label>
                <input matInput type="password" formControlName="confirmPassword" autocomplete="new-password" />
                @if (form.hasError('mismatch') && form.controls.confirmPassword.touched) {
                  <mat-error>{{ 'auth.resetPassword.mismatch' | t }}</mat-error>
                }
              </mat-form-field>
              <button matButton="filled" type="submit" [disabled]="submitting()">
                {{ (submitting() ? 'common.saving' : 'auth.resetPassword.submit') | t }}
              </button>
            </form>
          }
        </mat-card-content>
      </mat-card>
    </div>
  `,
  styles: `
    .auth-page { min-height: 100dvh; display: grid; place-items: center; padding: 24px 16px; }
    .auth-card { width: 100%; max-width: 420px; }
    .auth-form { display: flex; flex-direction: column; gap: 4px; margin-top: 8px; }
    .auth-note { color: var(--mat-sys-primary, #4f46e5); margin: 0 0 16px; }
    .auth-error { color: var(--mat-sys-error, #b3261e); margin: 0 0 12px; }
  `,
})
export class ResetPasswordComponent {
  private readonly fb = inject(FormBuilder);
  private readonly auth = inject(AuthService);
  private readonly route = inject(ActivatedRoute);

  readonly token = this.route.snapshot.queryParamMap.get('token') ?? '';
  readonly submitting = signal(false);
  readonly done = signal(false);
  readonly errorMessage = signal<string | null>(null);

  readonly form = this.fb.nonNullable.group(
    {
      newPassword: ['', [Validators.required]],
      confirmPassword: ['', [Validators.required]],
    },
    { validators: (group) => (group.get('newPassword')?.value === group.get('confirmPassword')?.value ? null : { mismatch: true }) },
  );

  submit(): void {
    if (this.form.invalid || this.submitting() || !this.token) {
      this.form.markAllAsTouched();
      return;
    }
    this.errorMessage.set(null);
    this.submitting.set(true);

    this.auth.resetPassword({ token: this.token, newPassword: this.form.getRawValue().newPassword }).subscribe({
      next: () => {
        this.submitting.set(false);
        this.done.set(true);
      },
      error: (err: unknown) => {
        this.submitting.set(false);
        const code = (err as { error?: { error_code?: string } })?.error?.error_code;
        this.errorMessage.set(
          code === 'invalid_or_expired_token' ? 'auth.resetPassword.invalidToken' : 'auth.resetPassword.policyError',
        );
      },
    });
  }
}
