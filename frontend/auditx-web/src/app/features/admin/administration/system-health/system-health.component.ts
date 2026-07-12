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

  readonly metrics = computed(() => {
    // Track the active language so labels re-resolve on toggle.
    this.i18n.lang();
    const h = this.health();
    if (!h) {
      return [];
    }
    return [
      {
        icon: 'group',
        label: this.i18n.translate('administration.health.activeUsers'),
        value: h.activeUserCount,
      },
      {
        icon: 'groups',
        label: this.i18n.translate('administration.health.totalUsers'),
        value: h.totalUserCount,
      },
      {
        icon: 'description',
        label: this.i18n.translate('administration.health.templates'),
        value: h.templateCount,
      },
      {
        icon: 'hub',
        label: this.i18n.translate('administration.health.integrations'),
        value: h.integrationCount,
      },
    ];
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
