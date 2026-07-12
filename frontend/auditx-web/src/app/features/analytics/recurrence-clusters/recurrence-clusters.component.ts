import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  signal,
} from '@angular/core';
import { DatePipe } from '@angular/common';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { IconComponent } from '../../../core/icons/icon.component';
import { MatTableModule } from '@angular/material/table';
import { RouterLink } from '@angular/router';

import { TranslatePipe } from '../../../core/i18n/translate.pipe';
import { AnalyticsService } from '../../../core/services/analytics.service';
import { EntityLookupService } from '../../../core/services/entity-lookup.service';
import { RecurrenceCluster } from '../../../core/models';
import { humanise } from '../format';
import { LoadingComponent } from '../../../shared/components/loading/loading.component';
import { EmptyStateComponent } from '../../../shared/components/empty-state/empty-state.component';
import { ErrorStateComponent } from '../../../shared/components/error-state/error-state.component';
import { PageHeaderComponent } from '../../../shared/components/page-header/page-header.component';
import { PaginatorComponent } from '../../../shared/components/paginator/paginator.component';
import { PageGuideComponent } from '../../../shared/components/page-guide/page-guide.component';
import { PageGuide } from '../../../core/models/page-guide.models';

type ViewState = 'loading' | 'ready' | 'error';

const DEFAULT_PAGE_SIZE = 25;

/** Contextual page guide for the recurrence-clusters list (drives the walkthrough + the About panel). */
const RECURRENCE_CLUSTERS_GUIDE: PageGuide = {
  id: 'analytics-recurrence-clusters',
  titleKey: 'analytics.nav.recurrenceClusters',
  purposeKey: 'analytics.recurrence.guide.purpose',
  descriptionKey: 'analytics.recurrence.guide.description',
  actionKeys: [
    'analytics.recurrence.guide.action.scan',
    'analytics.recurrence.guide.action.drill',
    'analytics.recurrence.guide.action.refresh',
    'analytics.recurrence.guide.action.act',
  ],
  sections: [
    { selector: '[data-guide="refresh"]', titleKey: 'analytics.recurrence.guide.section.refresh.title', bodyKey: 'analytics.recurrence.guide.section.refresh.body' },
    { selector: '.clusters__table-card', titleKey: 'analytics.recurrence.guide.section.table.title', bodyKey: 'analytics.recurrence.guide.section.table.body' },
    { selector: 'app-paginator', titleKey: 'analytics.recurrence.guide.section.paginator.title', bodyKey: 'analytics.recurrence.guide.section.paginator.body' },
  ],
  workflowKeys: [
    'analytics.recurrence.guide.flow.findings',
    'analytics.recurrence.guide.flow.close',
    'analytics.recurrence.guide.flow.detect',
    'analytics.recurrence.guide.flow.review',
    'analytics.recurrence.guide.flow.act',
  ],
  dependsOnKeys: [
    'analytics.recurrence.guide.dep.findings',
    'analytics.recurrence.guide.dep.entities',
    'analytics.recurrence.guide.dep.audits',
  ],
  usedByKeys: [
    'analytics.recurrence.guide.use.risks',
    'analytics.recurrence.guide.use.planning',
    'analytics.recurrence.guide.use.reports',
  ],
  businessRuleKeys: [
    'analytics.recurrence.guide.rule.closed',
    'analytics.recurrence.guide.rule.window',
    'analytics.recurrence.guide.rule.grouping',
  ],
  tipKeys: [
    'analytics.recurrence.guide.tip.drill',
    'analytics.recurrence.guide.tip.systemic',
  ],
  permissionKeys: [
    'analytics.recurrence.guide.perm.view',
    'analytics.recurrence.guide.perm.performance',
  ],
  faq: [
    { questionKey: 'analytics.recurrence.guide.faq.what.q', answerKey: 'analytics.recurrence.guide.faq.what.a' },
    { questionKey: 'analytics.recurrence.guide.faq.window.q', answerKey: 'analytics.recurrence.guide.faq.window.a' },
  ],
};

/** Offset-paged table of detected recurrence clusters; rows drill into detail. */
@Component({
  selector: 'app-recurrence-clusters',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    DatePipe,
    RouterLink,
    MatCardModule,
    MatTableModule,
    MatButtonModule,
    IconComponent,
    TranslatePipe,
    LoadingComponent,
    EmptyStateComponent,
    ErrorStateComponent,
    PageHeaderComponent,
    PaginatorComponent,
    PageGuideComponent,
  ],
  templateUrl: './recurrence-clusters.component.html',
  styleUrl: './recurrence-clusters.component.scss',
})
export class RecurrenceClustersComponent {
  private readonly service = inject(AnalyticsService);
  /** Resolves auditable-entity ids to names in the table. */
  readonly entityLookup = inject(EntityLookupService);

  readonly displayedColumns = [
    'entity',
    'category',
    'count',
    'window',
    'lastOccurred',
    'detected',
    'actions',
  ];

  readonly state = signal<ViewState>('loading');
  readonly clusters = signal<RecurrenceCluster[]>([]);
  readonly total = signal(0);
  readonly page = signal(1);
  readonly pageSize = signal(DEFAULT_PAGE_SIZE);
  /** In-flight fetch (page navigation) — disables the paginator without clearing the table. */
  readonly loading = signal(false);

  readonly humanise = humanise;

  readonly guide = RECURRENCE_CLUSTERS_GUIDE;

  readonly isEmpty = computed(
    () => this.state() === 'ready' && this.clusters().length === 0,
  );

  constructor() {
    this.fetchPage(1);
  }

  fetchPage(page: number): void {
    this.loading.set(true);
    this.service.recurrenceClusters(page, this.pageSize()).subscribe({
      next: (result) => {
        this.clusters.set(result.items);
        this.total.set(result.total);
        this.page.set(result.page);
        this.state.set('ready');
        this.loading.set(false);
      },
      error: () => {
        if (this.state() === 'loading') {
          this.state.set('error');
        }
        this.loading.set(false);
      },
    });
  }

  onPageChange(page: number): void {
    this.fetchPage(page);
  }

  onPageSizeChange(size: number): void {
    this.pageSize.set(size);
    this.fetchPage(1);
  }
}
