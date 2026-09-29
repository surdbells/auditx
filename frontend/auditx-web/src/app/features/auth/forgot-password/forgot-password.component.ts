import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';

import { AuthService } from '../../../core/services/auth.service';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';

/** Request a password-reset link. Always reports success (no account-existence disclosure). */
@Component({
  selector: 'app-forgot-password',
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
          <mat-card-title>{{ 'auth.forgotPassword.title' | t }}</mat-card-title>
        </mat-card-header>
        <mat-card-content>
          @if (sent()) {
            <p class="auth-note" role="status">{{ 'auth.forgotPassword.sent' | t }}</p>
            <a routerLink="/login" matButton>{{ 'auth.forgotPassword.backToLogin' | t }}</a>
          } @else {
            <p class="auth-help">{{ 'auth.forgotPassword.help' | t }}</p>
            <form [formGroup]="form" (ngSubmit)="submit()" class="auth-form">
              <mat-form-field appearance="outline">
                <mat-label>{{ 'auth.forgotPassword.identifier' | t }}</mat-label>
                <input matInput formControlName="usernameOrEmail" autocomplete="username" />
              </mat-form-field>
              <button matButton="filled" type="submit" [disabled]="submitting()">
                {{ (submitting() ? 'common.saving' : 'auth.forgotPassword.submit') | t }}
              </button>
              <a routerLink="/login" class="auth-link">{{ 'auth.forgotPassword.backToLogin' | t }}</a>
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
    .auth-help { color: var(--mat-sys-on-surface-variant, #59617a); margin: 0 0 8px; }
    .auth-note { color: var(--mat-sys-primary, #4f46e5); margin: 0 0 16px; }
    .auth-link { margin-top: 8px; text-align: center; }
  `,
})
export class ForgotPasswordComponent {
  private readonly fb = inject(FormBuilder);
  private readonly auth = inject(AuthService);

  readonly submitting = signal(false);
  readonly sent = signal(false);

  readonly form = this.fb.nonNullable.group({
    usernameOrEmail: ['', [Validators.required]],
  });

  submit(): void {
    if (this.form.invalid || this.submitting()) {
      this.form.markAllAsTouched();
      return;
    }
    this.submitting.set(true);
    this.auth.forgotPassword(this.form.getRawValue()).subscribe({
      // Success and failure look identical to the user (no enumeration).
      next: () => this.done(),
      error: () => this.done(),
    });
  }

  private done(): void {
    this.submitting.set(false);
    this.sent.set(true);
  }
}
