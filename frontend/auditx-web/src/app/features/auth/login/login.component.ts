import {
  ChangeDetectionStrategy,
  Component,
  inject,
  signal,
} from '@angular/core';
import {
  FormBuilder,
  ReactiveFormsModule,
  Validators,
} from '@angular/forms';
import { Router, ActivatedRoute } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';

import { AuthService } from '../../../core/services/auth.service';
import { SessionDto } from '../../../core/models';

@Component({
  selector: 'app-login',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    MatIconModule,
    MatProgressBarModule,
  ],
  templateUrl: './login.component.html',
  styleUrl: './login.component.scss',
})
export class LoginComponent {
  private readonly fb = inject(FormBuilder);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  readonly submitting = signal(false);
  readonly ssoBusy = signal(false);
  readonly errorMessage = signal<string | null>(null);
  readonly hidePassword = signal(true);

  readonly form = this.fb.nonNullable.group({
    username: ['', [Validators.required]],
    password: ['', [Validators.required]],
  });

  togglePassword(): void {
    this.hidePassword.update((v) => !v);
  }

  submit(): void {
    if (this.form.invalid || this.submitting()) {
      this.form.markAllAsTouched();
      return;
    }
    this.errorMessage.set(null);
    this.submitting.set(true);

    this.auth.login(this.form.getRawValue()).subscribe({
      next: (session) => {
        this.submitting.set(false);
        this.routeAfterLogin(session);
      },
      error: () => {
        this.submitting.set(false);
        // Opaque message per security requirements.
        this.errorMessage.set('Invalid username or password.');
      },
    });
  }

  useSso(): void {
    if (this.ssoBusy()) {
      return;
    }
    this.errorMessage.set(null);
    this.ssoBusy.set(true);

    this.auth.loginWithSso().subscribe({
      next: (session) => {
        this.ssoBusy.set(false);
        this.routeAfterLogin(session);
      },
      error: () => {
        this.ssoBusy.set(false);
        this.errorMessage.set(
          'Windows SSO is unavailable here. Please sign in with your username and password.',
        );
      },
    });
  }

  private routeAfterLogin(session: SessionDto): void {
    if (session.status === 'awaiting_role_assignment') {
      void this.router.navigate(['/awaiting-role']);
      return;
    }
    const returnUrl =
      this.route.snapshot.queryParamMap.get('returnUrl') ?? '/dashboard';
    void this.router.navigateByUrl(returnUrl);
  }
}
