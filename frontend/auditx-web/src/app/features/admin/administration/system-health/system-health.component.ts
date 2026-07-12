import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  signal,
} from '@angular/core';
import { DatePipe } from '@angular/common';
import { MatCardModule } from '@angular/material/card';
import { IconComponent } from '../../../../core/icons/icon.component';

import { AdministrationService } from '../../../../core/services/administration.service';
import { SystemHealth } from '../../../../core/models';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { TranslationService } from '../../../../core/i18n/translation.service';
import { LoadingComponent } from '../../../../shared/components/loading/loading.component';
import { ErrorStateComponent } from '../../../../shared/components/error-state/error-state.component';
import { PageGuideComponent } from '../../../../shared/components/page-guide/page-guide.component';
import { PageGuide } from '../../../../core/models/page-guide.models';

type ViewState = 'loading' | 'ready' | 'error';

/** Contextual page guide for system health (drives the walkthrough + the About panel). */
const SYSTEM_HEALTH_GUIDE: PageGuide = {
  id: 'administration-system-health',
  titleKey: 'administration.systemHealth.guide.pageTitle',
  purposeKey: 'administration.systemHealth.guide.purpose',
  descriptionKey: 'administration.systemHealth.guide.description',
  actionKeys: [
    'administration.systemHealth.guide.action.view',
    'administration.systemHealth.guide.action.metrics',
    'administration.systemHealth.guide.action.refresh',
  ],
  sections: [
    { selector: '.health__status', titleKey: 'administration.systemHealth.guide.section.status.title', bodyKey: 'administration.systemHealth.guide.section.status.body' },
    { selector: '.health__grid', titleKey: 'administration.systemHealth.guide.section.metrics.title', bodyKey: 'administration.systemHealth.guide.section.metrics.body' },
  ],
  workflowKeys: [
    'administration.systemHealth.guide.flow.configure',
    'administration.systemHealth.guide.flow.operate',
    'administration.systemHealth.guide.flow.snapshot',
    'administration.systemHealth.guide.flow.review',
  ],
  dependsOnKeys: [
    'administration.systemHealth.guide.dep.users',
    'administration.systemHealth.guide.dep.templates',
    'administration.systemHealth.guide.dep.integrations',
    'administration.systemHealth.guide.dep.api',
  ],
  usedByKeys: [
    'administration.systemHealth.guide.use.admin',
    'administration.systemHealth.guide.use.support',
    'administration.systemHealth.guide.use.releases',
  ],
  businessRuleKeys: [
    'administration.systemHealth.guide.rule.permission',
    'administration.systemHealth.guide.rule.readonly',
    'administration.systemHealth.guide.rule.snapshot',
    'administration.systemHealth.guide.rule.activeUsers',
  ],
  tipKeys: [
    'administration.systemHealth.guide.tip.retry',
    'administration.systemHealth.guide.tip.degraded',
  ],
  permissionKeys: [
    'administration.systemHealth.guide.perm.admin',
    'administration.systemHealth.guide.perm.ops',
  ],
  faq: [
    { questionKey: 'administration.systemHealth.guide.faq.status.q', answerKey: 'administration.systemHealth.guide.faq.status.a' },
    { questionKey: 'administration.systemHealth.guide.faq.fresh.q', answerKey: 'administration.systemHealth.guide.faq.fresh.a' },
  ],
};

@Component({
  selector: 'app-system-health',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    DatePipe,
    MatCardModule,
    IconComponent,
    TranslatePipe,
    LoadingComponent,
    ErrorStateComponent,
    PageGuideComponent,
  ],
  templateUrl: './system-health.component.html',
  styleUrl: './system-health.component.scss',
})
export class SystemHealthComponent {
  private readonly admin = inject(AdministrationService);
  private readonly i18n = inject(TranslationService);

  readonly guide = SYSTEM_HEALTH_GUIDE;

  readonly state = signal<ViewState>('loading');
  readonly health = signal<SystemHealth | null>(null);

  /** Per-metric icon, keyed by the backend label. */
  private static readonly METRIC_ICONS: Record<string, string> = {
    'Active users': 'how_to_reg',
    'Total users': 'groups',
    Audits: 'assignment',
    Exceptions: 'report_problem',
    Controls: 'fact_check',
    Regulations: 'account_balance',
    Risks: 'crisis_alert',
    'Checklist templates': 'description',
    'Active integrations': 'hub',
    'Webhook subscriptions': 'webhook',
  };

  readonly checks = computed(() => this.health()?.checks ?? []);

  readonly metrics = computed(() =>
    (this.health()?.metrics ?? []).map((m) => ({
      icon: SystemHealthComponent.METRIC_ICONS[m.label] ?? 'insights',
      label: m.label,
      value: m.value,
    })),
  );

  /** Human uptime (e.g. "3d 4h", "12m"). */
  readonly uptimeText = computed(() => {
    const s = this.health()?.uptimeSeconds ?? 0;
    const d = Math.floor(s / 86400);
    const h = Math.floor((s % 86400) / 3600);
    const m = Math.floor((s % 3600) / 60);
    if (d > 0) {
      return `${d}d ${h}h`;
    }
    if (h > 0) {
      return `${h}h ${m}m`;
    }
    return `${m}m`;
  });

  constructor() {
    this.fetch();
  }

  fetch(): void {
    this.state.set('loading');
    this.admin.getSystemHealth().subscribe({
      next: (h) => {
        this.health.set(h);
        this.state.set('ready');
      },
      error: () => this.state.set('error'),
    });
  }
}
