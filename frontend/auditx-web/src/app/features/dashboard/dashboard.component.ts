import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { DatePipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatChipsModule } from '@angular/material/chips';
import { MatIconModule } from '@angular/material/icon';

import { AuthService } from '../../core/services/auth.service';
import { Permissions } from '../../core/permissions';
import { PageHeaderComponent } from '../../shared/components/page-header/page-header.component';
import { ChartDatum, DonutChartComponent } from '../../shared/charts';

interface DashboardCard {
  title: string;
  description: string;
  icon: string;
  route: string;
  cta: string;
  permissions: string[];
}

@Component({
  selector: 'app-dashboard',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    DatePipe,
    RouterLink,
    MatCardModule,
    MatIconModule,
    MatButtonModule,
    MatChipsModule,
    PageHeaderComponent,
    DonutChartComponent,
  ],
  templateUrl: './dashboard.component.html',
  styleUrl: './dashboard.component.scss',
})
export class DashboardComponent {
  private readonly auth = inject(AuthService);

  readonly session = this.auth.session;
  readonly firstName = computed(() => this.session()?.firstName ?? 'there');
  readonly roleCount = computed(() => this.session()?.roles.length ?? 0);
  readonly permissionCount = computed(
    () => this.session()?.permissions.length ?? 0,
  );

  /**
   * A donut of the signed-in user's granted permissions bucketed by access verb
   * (View / Manage / Configure / Approve / Other). Purely derived from the
   * session already in memory — no extra API call — so the home page carries a
   * real visual without new data dependencies.
   */
  readonly accessProfile = computed<ChartDatum[]>(() => {
    const perms = this.session()?.permissions ?? [];
    const buckets: Record<string, number> = {
      View: 0,
      Manage: 0,
      Configure: 0,
      Approve: 0,
      Other: 0,
    };
    for (const p of perms) {
      const verb = ['View', 'Manage', 'Configure', 'Approve'].find((v) =>
        p.startsWith(v),
      );
      buckets[verb ?? 'Other'] += 1;
    }
    return Object.entries(buckets)
      .filter(([, count]) => count > 0)
      .map(([label, value]) => ({ label, value }));
  });

  private readonly allCards: DashboardCard[] = [
    {
      title: 'User Management',
      description:
        'Search the directory, review accounts, assign roles and manage delegations.',
      icon: 'group',
      route: '/admin/users',
      cta: 'Manage users',
      permissions: [Permissions.ManageUsers],
    },
    {
      title: 'Roles & Permissions',
      description:
        'Define roles, compose permissions by module and configure inheritance.',
      icon: 'admin_panel_settings',
      route: '/admin/roles',
      cta: 'Manage roles',
      permissions: [Permissions.ManageRoles],
    },
    {
      title: 'Maker-Checker Queue',
      description:
        'Review and approve or reject sensitive changes awaiting a second authoriser.',
      icon: 'fact_check',
      route: '/admin/maker-checker',
      cta: 'Open queue',
      permissions: [
        Permissions.ManageRoles,
        Permissions.ManageUsers,
        Permissions.ApproveMakerChecker,
      ],
    },
  ];

  readonly cards = computed(() =>
    this.allCards.filter(
      (c) =>
        c.permissions.length === 0 ||
        this.auth.hasAnyPermission(...c.permissions),
    ),
  );

  readonly sessionExpiry = computed(() => {
    const expiresAt = this.session()?.expiresAt;
    if (!expiresAt) {
      return null;
    }
    const date = new Date(expiresAt);
    return Number.isNaN(date.getTime()) ? null : date;
  });
}
