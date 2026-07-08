import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { BreakpointObserver, Breakpoints } from '@angular/cdk/layout';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { MatSidenavContainer, MatSidenavModule } from '@angular/material/sidenav';
import { MatToolbarModule } from '@angular/material/toolbar';
import { MatTooltipModule } from '@angular/material/tooltip';
import { map } from 'rxjs';

import { AuthService } from '../../core/services/auth.service';
import { NotificationService } from '../../core/services/notification.service';
import { Permissions } from '../../core/permissions';
import { TranslatePipe } from '../../core/i18n/translate.pipe';
import { LanguageSwitcherComponent } from '../../core/i18n/language-switcher.component';

interface NavItem {
  /** Translation key resolved with the `t` pipe. */
  labelKey: string;
  icon: string;
  route: string;
  /** Permission keys; item is shown when the user holds any of them (empty = always). */
  permissions: string[];
}

interface NavSection {
  /** Section header translation key. */
  titleKey: string;
  items: NavItem[];
}

const COLLAPSE_KEY = 'auditx.nav.collapsed';

@Component({
  selector: 'app-main-layout',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    RouterOutlet,
    RouterLink,
    RouterLinkActive,
    MatToolbarModule,
    MatSidenavModule,
    MatIconModule,
    MatButtonModule,
    MatMenuModule,
    MatTooltipModule,
    TranslatePipe,
    LanguageSwitcherComponent,
  ],
  templateUrl: './main-layout.component.html',
  styleUrl: './main-layout.component.scss',
})
export class MainLayoutComponent {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly notify = inject(NotificationService);
  private readonly breakpoints = inject(BreakpointObserver);

  private readonly sidenavContainer = viewChild(MatSidenavContainer);

  readonly displayName = this.auth.displayName;
  readonly session = this.auth.session;

  /** Personal account settings surface (the bottom profile menu's "Settings"). */
  readonly settingsRoute = '/account/notification-preferences';

  readonly isHandset = toSignal(
    this.breakpoints
      .observe([Breakpoints.Handset, Breakpoints.Small])
      .pipe(map((result) => result.matches)),
    { initialValue: false },
  );

  // On desktop the template binding forces the drawer open; on handset it starts
  // closed so the user lands on content, and the hamburger opens it over the page.
  readonly sidenavOpened = signal(false);

  /** Icon-only rail on desktop; persisted so the choice survives reloads. */
  readonly collapsed = signal(this.readCollapsed());

  private readonly allSections: NavSection[] = [
    {
      titleKey: 'nav.section.overview',
      items: [{ labelKey: 'nav.dashboard', icon: 'dashboard', route: '/dashboard', permissions: [] }],
    },
    {
      titleKey: 'nav.section.audit',
      items: [
        { labelKey: 'nav.universe', icon: 'account_tree', route: '/audit-universe', permissions: [Permissions.ViewUniverse] },
        { labelKey: 'nav.planning', icon: 'event_note', route: '/planning', permissions: [Permissions.ViewPlan] },
        { labelKey: 'nav.audits', icon: 'assignment', route: '/audits', permissions: [Permissions.ViewAudits] },
        { labelKey: 'nav.coverage', icon: 'grid_view', route: '/coverage', permissions: [Permissions.ViewCoverage] },
        { labelKey: 'nav.exceptions', icon: 'report_problem', route: '/exceptions', permissions: [Permissions.ViewExceptions] },
      ],
    },
    {
      titleKey: 'nav.section.committee',
      items: [
        { labelKey: 'nav.acPacks', icon: 'inventory_2', route: '/ac/packs', permissions: [Permissions.ViewACPacks] },
        { labelKey: 'nav.acDashboard', icon: 'space_dashboard', route: '/ac/dashboard', permissions: [Permissions.ACMember] },
        { labelKey: 'nav.acActionItems', icon: 'checklist', route: '/ac/action-items', permissions: [Permissions.ACMember] },
        { labelKey: 'nav.plansAwaiting', icon: 'how_to_vote', route: '/ac/plans-awaiting', permissions: [Permissions.ACMember] },
      ],
    },
    {
      titleKey: 'nav.section.insights',
      items: [
        { labelKey: 'nav.analytics', icon: 'analytics', route: '/analytics', permissions: [Permissions.ViewAnalytics] },
      ],
    },
    {
      titleKey: 'nav.section.sanctions',
      items: [
        { labelKey: 'nav.sanctions', icon: 'gavel', route: '/admin/sanctions', permissions: [Permissions.ViewSanctions] },
        { labelKey: 'nav.sanctionsGrid', icon: 'grid_on', route: '/admin/sanctions/grid', permissions: [Permissions.ManageGrid] },
      ],
    },
    {
      titleKey: 'nav.section.access',
      items: [
        { labelKey: 'nav.users', icon: 'group', route: '/admin/users', permissions: [Permissions.ManageUsers] },
        { labelKey: 'nav.roles', icon: 'admin_panel_settings', route: '/admin/roles', permissions: [Permissions.ManageRoles] },
        {
          labelKey: 'nav.makerChecker',
          icon: 'fact_check',
          route: '/admin/maker-checker',
          permissions: [Permissions.ManageRoles, Permissions.ManageUsers, Permissions.ApproveMakerChecker],
        },
      ],
    },
    {
      titleKey: 'nav.section.configuration',
      items: [
        { labelKey: 'nav.templates', icon: 'description', route: '/admin/templates', permissions: [Permissions.ViewTemplates] },
        { labelKey: 'nav.reportTemplates', icon: 'summarize', route: '/admin/report-templates', permissions: [Permissions.ConfigureReports] },
        { labelKey: 'nav.notifications', icon: 'notifications', route: '/admin/notifications', permissions: [Permissions.ConfigureNotifications] },
        {
          labelKey: 'nav.configuration',
          icon: 'tune',
          route: '/admin/configuration',
          permissions: [Permissions.ViewConfig, Permissions.ManageConfiguration],
        },
        { labelKey: 'nav.referenceData', icon: 'list_alt', route: '/admin/reference-data', permissions: [Permissions.ManageConfiguration] },
      ],
    },
    {
      titleKey: 'nav.section.platform',
      items: [
        { labelKey: 'nav.integrations', icon: 'hub', route: '/admin/integrations', permissions: [Permissions.ViewIntegrations] },
        { labelKey: 'nav.webhooks', icon: 'webhook', route: '/admin/webhooks', permissions: [Permissions.ConfigureWebhooks, Permissions.AdminOps] },
        { labelKey: 'nav.auditTrail', icon: 'history', route: '/admin/audit-trail', permissions: [Permissions.ViewAuditTrail] },
        { labelKey: 'nav.evidenceIntegrity', icon: 'verified_user', route: '/admin/evidence-integrity', permissions: [Permissions.AdminOps] },
        {
          labelKey: 'nav.administration',
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
      ],
    },
  ];

  /** Sections with their permitted items; sections with nothing visible are dropped. */
  readonly navSections = computed<NavSection[]>(() =>
    this.allSections
      .map((section) => ({
        titleKey: section.titleKey,
        items: section.items.filter(
          (item) =>
            item.permissions.length === 0 ||
            this.auth.hasAnyPermission(...item.permissions),
        ),
      }))
      .filter((section) => section.items.length > 0),
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

  /** Collapse/expand the desktop rail; persist and recompute the content margin. */
  toggleCollapsed(): void {
    const next = !this.collapsed();
    this.collapsed.set(next);
    try {
      localStorage.setItem(COLLAPSE_KEY, next ? '1' : '0');
    } catch {
      // Storage unavailable (private mode) — the choice just won't persist.
    }
    // The rail width is CSS-driven; let it apply, then re-measure the pushed content.
    setTimeout(() => this.sidenavContainer()?.updateContentMargins(), 0);
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

  private readCollapsed(): boolean {
    try {
      return localStorage.getItem(COLLAPSE_KEY) === '1';
    } catch {
      return false;
    }
  }
}
