import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  signal,
} from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { IconComponent } from '../../../core/icons/icon.component';
import { RouterLink } from '@angular/router';

import { TranslatePipe } from '../../../core/i18n/translate.pipe';
import { AnalyticsService } from '../../../core/services/analytics.service';
import { DashboardListItem } from '../../../core/models';
import { LoadingComponent } from '../../../shared/components/loading/loading.component';
import { EmptyStateComponent } from '../../../shared/components/empty-state/empty-state.component';
import { ErrorStateComponent } from '../../../shared/components/error-state/error-state.component';
import { PageHeaderComponent } from '../../../shared/components/page-header/page-header.component';
import { PageGuideComponent } from '../../../shared/components/page-guide/page-guide.component';
import { PageGuide } from '../../../core/models/page-guide.models';

type ViewState = 'loading' | 'ready' | 'error';

/** Contextual page guide for the analytics dashboards list (drives the walkthrough + the About panel). */
const DASHBOARDS_GUIDE: PageGuide = {
  id: 'analytics-dashboards-list',
  titleKey: 'analytics.list.title',
  purposeKey: 'analytics.dashboardsList.guide.purpose',
  descriptionKey: 'analytics.dashboardsList.guide.description',
  actionKeys: [
    'analytics.dashboardsList.guide.action.open',
    'analytics.dashboardsList.guide.action.build',
    'analytics.dashboardsList.guide.action.clusters',
    'analytics.dashboardsList.guide.action.refresh',
  ],
  sections: [
    { selector: '[data-guide="actions"]', titleKey: 'analytics.dashboardsList.guide.section.actions.title', bodyKey: 'analytics.dashboardsList.guide.section.actions.body' },
    { selector: '.dashboards__grid', titleKey: 'analytics.dashboardsList.guide.section.grid.title', bodyKey: 'analytics.dashboardsList.guide.section.grid.body' },
    { selector: '.dashboards__card', titleKey: 'analytics.dashboardsList.guide.section.card.title', bodyKey: 'analytics.dashboardsList.guide.section.card.body' },
  ],
  workflowKeys: [
    'analytics.dashboardsList.guide.flow.capture',
    'analytics.dashboardsList.guide.flow.snapshot',
    'analytics.dashboardsList.guide.flow.compose',
    'analytics.dashboardsList.guide.flow.browse',
    'analytics.dashboardsList.guide.flow.act',
  ],
  dependsOnKeys: [
    'analytics.dashboardsList.guide.dep.snapshots',
    'analytics.dashboardsList.guide.dep.widgets',
    'analytics.dashboardsList.guide.dep.modules',
    'analytics.dashboardsList.guide.dep.permission',
  ],
  usedByKeys: [
    'analytics.dashboardsList.guide.use.review',
    'analytics.dashboardsList.guide.use.reports',
    'analytics.dashboardsList.guide.use.trends',
  ],
  businessRuleKeys: [
    'analytics.dashboardsList.guide.rule.view',
    'analytics.dashboardsList.guide.rule.configure',
    'analytics.dashboardsList.guide.rule.slug',
    'analytics.dashboardsList.guide.rule.widgets',
  ],
  tipKeys: [
    'analytics.dashboardsList.guide.tip.build',
    'analytics.dashboardsList.guide.tip.refresh',
    'analytics.dashboardsList.guide.tip.clusters',
  ],
  permissionKeys: [
    'analytics.dashboardsList.guide.perm.viewer',
    'analytics.dashboardsList.guide.perm.builder',
    'analytics.dashboardsList.guide.perm.admin',
  ],
  faq: [
    { questionKey: 'analytics.dashboardsList.guide.faq.empty.q', answerKey: 'analytics.dashboardsList.guide.faq.empty.a' },
    { questionKey: 'analytics.dashboardsList.guide.faq.custom.q', answerKey: 'analytics.dashboardsList.guide.faq.custom.a' },
  ],
};

/** Lists the analytics dashboards the caller may see, as navigable cards. */
@Component({
  selector: 'app-dashboards-list',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    RouterLink,
    MatCardModule,
    MatButtonModule,
    IconComponent,
    TranslatePipe,
    LoadingComponent,
    EmptyStateComponent,
    ErrorStateComponent,
    PageHeaderComponent,
    PageGuideComponent,
  ],
  templateUrl: './dashboards-list.component.html',
  styleUrl: './dashboards-list.component.scss',
})
export class DashboardsListComponent {
  private readonly service = inject(AnalyticsService);

  readonly guide = DASHBOARDS_GUIDE;

  readonly state = signal<ViewState>('loading');
  readonly dashboards = signal<DashboardListItem[]>([]);

  readonly isEmpty = computed(
    () => this.state() === 'ready' && this.dashboards().length === 0,
  );

  constructor() {
    this.fetch();
  }

  fetch(): void {
    this.state.set('loading');
    this.service.listDashboards().subscribe({
      next: (items) => {
        this.dashboards.set(items);
        this.state.set('ready');
      },
      error: () => this.state.set('error'),
    });
  }
}
