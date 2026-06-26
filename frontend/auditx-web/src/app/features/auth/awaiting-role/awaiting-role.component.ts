import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { Router } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';

import { AuthService } from '../../../core/services/auth.service';
import { NotificationService } from '../../../core/services/notification.service';

@Component({
  selector: 'app-awaiting-role',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatCardModule, MatButtonModule, MatIconModule],
  templateUrl: './awaiting-role.component.html',
  styleUrl: './awaiting-role.component.scss',
})
export class AwaitingRoleComponent {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly notify = inject(NotificationService);

  readonly session = this.auth.session;

  refresh(): void {
    this.auth.loadSession().subscribe({
      next: (session) => {
        if (session.status !== 'awaiting_role_assignment') {
          void this.router.navigate(['/dashboard']);
        } else {
          this.notify.info('No roles have been assigned yet.');
        }
      },
    });
  }

  logout(): void {
    this.auth.logout().subscribe({
      next: () => void this.router.navigate(['/login']),
      error: () => {
        this.auth.clearSession();
        void this.router.navigate(['/login']);
      },
    });
  }
}
