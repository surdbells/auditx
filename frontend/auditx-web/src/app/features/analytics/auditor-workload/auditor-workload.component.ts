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
import { MatTableModule } from '@angular/material/table';
import { RouterLink } from '@angular/router';

import { TranslatePipe } from '../../../core/i18n/translate.pipe';
import { AnalyticsService } from '../../../core/services/analytics.service';
import { UserLookupService } from '../../../core/services/user-lookup.service';
import { AuditorWorkloadRow } from '../../../core/models';
import { metricOrDash, percent } from '../format';
import { LoadingComponent } from '../../../shared/components/loading/loading.component';
import { EmptyStateComponent } from '../../../shared/components/empty-state/empty-state.component';
import { ErrorStateComponent } from '../../../shared/components/error-state/error-state.component';
import { PageHeaderComponent } from '../../../shared/components/page-header/page-header.component';
import { PageGuideComponent } from '../../../shared/components/page-guide/page-guide.component';
import { PageGuide } from '../../../core/models/page-guide.models';

type ViewState = 'loading' | 'ready' | 'error';

/** Contextual page guide for auditor workload (drives the walkthrough + the About panel). */
const WORKLOAD_GUIDE: PageGuide = {
  id: 'analytics-auditor-workload',
  titleKey: 'analytics.workload.title',
  purposeKey: 'analytics.workload.guide.purpose',
  descriptionKey: 'analytics.workload.guide.description',
  actionKeys: [
    'analytics.workload.guide.action.scan',
    'analytics.workload.guide.action.capacity',
    'analytics.workload.guide.action.refresh',
  ],
  sections: [
    { selector: '[data-guide="table"]', titleKey: 'analytics.workload.guide.section.table.title', bodyKey: 'analytics.workload.guide.section.table.body' },
    { selector: '[data-guide="refresh"]', titleKey: 'analytics.workload.guide.section.refresh.title', bodyKey: 'analytics.workload.guide.section.refresh.body' },
  ],
  workflowKeys: [
    'analytics.workload.guide.flow.capacity',
    'analytics.workload.guide.flow.plan',
    'analytics.workload.guide.flow.assign',
    'analytics.workload.guide.flow.aggregate',
    'analytics.workload.guide.flow.rebalance',
  ],
  dependsOnKeys: [
    'analytics.workload.guide.dep.plans',
    'analytics.workload.guide.dep.users',
  ],
  usedByKeys: [
    'analytics.workload.guide.use.planning',
    'analytics.workload.guide.use.reports',
  ],
  businessRuleKeys: [
    'analytics.workload.guide.rule.open',
    'analytics.workload.guide.rule.utilisation',
    'analytics.workload.guide.rule.over',
  ],
  tipKeys: [
    'analytics.workload.guide.tip.capacity',
    'analytics.workload.guide.tip.over',
  ],
  permissionKeys: [
    'analytics.workload.guide.perm.analytics',
    'analytics.workload.guide.perm.manage',
  ],
  faq: [
    { questionKey: 'analytics.workload.guide.faq.capacity.q', answerKey: 'analytics.workload.guide.faq.capacity.a' },
    { questionKey: 'analytics.workload.guide.faq.open.q', answerKey: 'analytics.workload.guide.faq.open.a' },
  ],
};

/**
 * Auditor workload analytics (ViewAnalytics): open planned effort (Planned / InProgress plan items) rolled up by
 * assigned lead and compared against each lead's declared capacity, so over- and under-committed auditors surface
 * for resource re-planning.
 */
@Component({
  selector: 'app-auditor-workload',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
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
    PageGuideComponent,
  ],
  templateUrl: './auditor-workload.component.html',
  styleUrl: './auditor-workload.component.scss',
})
export class AuditorWorkloadComponent {
  private readonly service = inject(AnalyticsService);
  /** Resolves audit-lead user ids to display names. */
  readonly userLookup = inject(UserLookupService);

  readonly guide = WORKLOAD_GUIDE;

  readonly columns = ['auditor', 'items', 'planned', 'capacity', 'utilisation', 'status'];

  readonly state = signal<ViewState>('loading');
  readonly rows = signal<AuditorWorkloadRow[]>([]);

  readonly percent = percent;
  readonly metricOrDash = metricOrDash;

  readonly isEmpty = computed(() => this.state() === 'ready' && this.rows().length === 0);

  constructor() {
    this.fetch();
  }

  fetch(): void {
    this.state.set('loading');
    this.service.auditorWorkload().subscribe({
      next: (rows) => {
        this.rows.set(rows);
        this.state.set('ready');
      },
      error: () => this.state.set('error'),
    });
  }

  /** The i18n key + badge state for a row: no declared capacity, over-committed, or within capacity. */
  statusKey(row: AuditorWorkloadRow): string {
    if (row.capacityDays === null) {
      return 'analytics.workload.status.noCapacity';
    }
    return row.overCommitted ? 'analytics.workload.status.over' : 'analytics.workload.status.ok';
  }

  statusTone(row: AuditorWorkloadRow): 'over' | 'ok' | 'none' {
    if (row.capacityDays === null) {
      return 'none';
    }
    return row.overCommitted ? 'over' : 'ok';
  }
}
