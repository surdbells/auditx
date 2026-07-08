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
import { TranslatePipe } from '../../core/i18n/translate.pipe';
import { TranslationService } from '../../core/i18n/translation.service';

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
    TranslatePipe,
  ],
  templateUrl: './dashboard.component.html',
  styleUrl: './dashboard.component.scss',
})
export class DashboardComponent {
  private readonly auth = inject(AuthService);
  private readonly i18n = inject(TranslationService);

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
      .map(([label, value]) => ({
        label: this.i18n.translate(`dashboard.access.verb.${label}`),
        value,
      }));
  });

  private readonly allCards: DashboardCard[] = [
    {
      title: 'dashboard.card.users.title',
      description: 'dashboard.card.users.desc',
      icon: 'group',
      route: '/admin/users',
      cta: 'dashboard.card.users.cta',
      permissions: [Permissions.ManageUsers],
    },
    {
      title: 'dashboard.card.roles.title',
      description: 'dashboard.card.roles.desc',
      icon: 'admin_panel_settings',
      route: '/admin/roles',
      cta: 'dashboard.card.roles.cta',
      permissions: [Permissions.ManageRoles],
    },
    {
      title: 'dashboard.card.makerChecker.title',
      description: 'dashboard.card.makerChecker.desc',
      icon: 'fact_check',
      route: '/admin/maker-checker',
      cta: 'dashboard.card.makerChecker.cta',
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
