import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  signal,
} from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { BreakpointObserver, Breakpoints } from '@angular/cdk/layout';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatListModule } from '@angular/material/list';
import { MatMenuModule } from '@angular/material/menu';
import { MatSidenavModule } from '@angular/material/sidenav';
import { MatToolbarModule } from '@angular/material/toolbar';
import { map } from 'rxjs';

import { AuthService } from '../../core/services/auth.service';
import { NotificationService } from '../../core/services/notification.service';
import { Permissions } from '../../core/permissions';

interface NavItem {
  label: string;
  icon: string;
  route: string;
  /** Permission keys; item is shown when the user holds any of them (empty = always). */
  permissions: string[];
}

@Component({
  selector: 'app-main-layout',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    RouterOutlet,
    RouterLink,
    RouterLinkActive,
    MatToolbarModule,
    MatSidenavModule,
    MatListModule,
    MatIconModule,
    MatButtonModule,
    MatMenuModule,
  ],
  templateUrl: './main-layout.component.html',
  styleUrl: './main-layout.component.scss',
})
export class MainLayoutComponent {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly notify = inject(NotificationService);
  private readonly breakpoints = inject(BreakpointObserver);

  readonly displayName = this.auth.displayName;
  readonly session = this.auth.session;

  readonly isHandset = toSignal(
    this.breakpoints
      .observe([Breakpoints.Handset, Breakpoints.Small])
      .pipe(map((result) => result.matches)),
    { initialValue: false },
  );

  readonly sidenavOpened = signal(true);

  private readonly allNavItems: NavItem[] = [
    { label: 'Dashboard', icon: 'dashboard', route: '/dashboard', permissions: [] },
    {
      label: 'Users',
      icon: 'group',
      route: '/admin/users',
      permissions: [Permissions.ManageUsers],
    },
    {
      label: 'Roles',
      icon: 'admin_panel_settings',
      route: '/admin/roles',
      permissions: [Permissions.ManageRoles],
    },
    {
      label: 'Templates',
      icon: 'description',
      route: '/admin/templates',
      permissions: [Permissions.ViewTemplates],
    },
    {
      label: 'Audit Universe',
      icon: 'account_tree',
      route: '/audit-universe',
      permissions: [Permissions.ViewUniverse],
    },
    {
      label: 'Planning',
      icon: 'event_note',
      route: '/planning',
      permissions: [Permissions.ViewPlan],
    },
    {
      label: 'Audits',
      icon: 'assignment',
      route: '/audits',
      permissions: [Permissions.ViewAudits],
    },
    {
      label: 'Coverage',
      icon: 'grid_view',
      route: '/coverage',
      permissions: [Permissions.ViewCoverage],
    },
    {
      label: 'Exceptions',
      icon: 'report_problem',
      route: '/exceptions',
      permissions: [Permissions.ViewExceptions],
    },
    {
      label: 'Maker-Checker',
      icon: 'fact_check',
      route: '/admin/maker-checker',
      permissions: [
        Permissions.ManageRoles,
        Permissions.ManageUsers,
        Permissions.ApproveMakerChecker,
      ],
    },
    {
      label: 'Integrations',
      icon: 'hub',
      route: '/admin/integrations',
      permissions: [Permissions.ViewIntegrations],
    },
    {
      label: 'Webhooks',
      icon: 'webhook',
      route: '/admin/webhooks',
      permissions: [Permissions.ConfigureWebhooks, Permissions.AdminOps],
    },
    {
      label: 'Notifications',
      icon: 'notifications',
      route: '/admin/notifications',
      permissions: [Permissions.ConfigureNotifications],
    },
    {
      label: 'Report Templates',
      icon: 'summarize',
      route: '/admin/report-templates',
      permissions: [Permissions.ConfigureReports],
    },
    {
      label: 'Sanctions',
      icon: 'gavel',
      route: '/admin/sanctions',
      permissions: [Permissions.ViewSanctions],
    },
    {
      label: 'Sanctions Grid',
      icon: 'grid_on',
      route: '/admin/sanctions/grid',
      permissions: [Permissions.ManageGrid],
    },
    {
      label: 'Audit Trail',
      icon: 'history',
      route: '/admin/audit-trail',
      permissions: [Permissions.ViewAuditTrail],
    },
    {
      label: 'Evidence Integrity',
      icon: 'verified_user',
      route: '/admin/evidence-integrity',
      permissions: [Permissions.AdminOps],
    },
    {
      label: 'Administration',
      icon: 'settings',
      route: '/admin/administration',
      permissions: [
        Permissions.ViewBankSettings,
        Permissions.ViewSystemHealth,
        Permissions.ManageBankSettings,
        Permissions.ConfigureLimits,
        Permissions.ManageUsers,
        Permissions.ManageSupportChannel,
        Permissions.InstallReleases,
        Permissions.ManageRetention,
        Permissions.ExecRestore,
      ],
    },
  ];

  readonly navItems = computed(() =>
    this.allNavItems.filter(
      (item) =>
        item.permissions.length === 0 ||
        this.auth.hasAnyPermission(...item.permissions),
    ),
  );

  readonly initials = computed(() => {
    const name = this.displayName().trim();
    if (!name) {
      return '?';
    }
    const parts = name.split(/\s+/);
    const first = parts[0]?.[0] ?? '';
    const last = parts.length > 1 ? (parts[parts.length - 1]?.[0] ?? '') : '';
    return (first + last).toUpperCase();
  });

  toggleSidenav(): void {
    this.sidenavOpened.update((v) => !v);
  }

  onNavigate(): void {
    if (this.isHandset()) {
      this.sidenavOpened.set(false);
    }
  }

  logout(): void {
    this.auth.logout().subscribe({
      next: () => {
        this.notify.info('You have been signed out.');
        void this.router.navigate(['/login']);
      },
      error: () => {
        // Even if the call fails, clear local state and return to login.
        this.auth.clearSession();
        void this.router.navigate(['/login']);
      },
    });
  }
}
