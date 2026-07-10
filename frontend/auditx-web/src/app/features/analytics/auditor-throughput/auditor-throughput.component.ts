import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  signal,
} from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { MatTableModule } from '@angular/material/table';
import { RouterLink } from '@angular/router';

import { TranslatePipe } from '../../../core/i18n/translate.pipe';
import { AnalyticsService } from '../../../core/services/analytics.service';
import { UserLookupService } from '../../../core/services/user-lookup.service';
import { AuditorThroughputRow } from '../../../core/models';
import { LoadingComponent } from '../../../shared/components/loading/loading.component';
import { EmptyStateComponent } from '../../../shared/components/empty-state/empty-state.component';
import { ErrorStateComponent } from '../../../shared/components/error-state/error-state.component';
import { PageHeaderComponent } from '../../../shared/components/page-header/page-header.component';
import { PageGuideComponent } from '../../../shared/components/page-guide/page-guide.component';
import { PageGuide } from '../../../core/models/page-guide.models';

type ViewState = 'loading' | 'ready' | 'error';

/** Contextual page guide for the auditor-throughput view (drives the walkthrough + the About panel). */
const THROUGHPUT_GUIDE: PageGuide = {
  id: 'analytics-auditor-throughput',
  titleKey: 'analytics.throughput.title',
  purposeKey: 'analytics.throughput.guide.purpose',
  descriptionKey: 'analytics.throughput.guide.description',
  actionKeys: [
    'analytics.throughput.guide.action.review',
    'analytics.throughput.guide.action.compare',
    'analytics.throughput.guide.action.refresh',
  ],
  sections: [
    { selector: '[data-guide="controls"]', titleKey: 'analytics.throughput.guide.section.controls.title', bodyKey: 'analytics.throughput.guide.section.controls.body' },
    { selector: '.throughput__table', titleKey: 'analytics.throughput.guide.section.table.title', bodyKey: 'analytics.throughput.guide.section.table.body' },
  ],
  workflowKeys: [
    'analytics.throughput.guide.flow.respond',
    'analytics.throughput.guide.flow.evidence',
    'analytics.throughput.guide.flow.raise',
    'analytics.throughput.guide.flow.aggregate',
    'analytics.throughput.guide.flow.review',
  ],
  dependsOnKeys: [
    'analytics.throughput.guide.dep.responses',
    'analytics.throughput.guide.dep.evidence',
    'analytics.throughput.guide.dep.exceptions',
  ],
  usedByKeys: [
    'analytics.throughput.guide.use.appraisal',
    'analytics.throughput.guide.use.capacity',
    'analytics.throughput.guide.use.reports',
  ],
  businessRuleKeys: [
    'analytics.throughput.guide.rule.team',
    'analytics.throughput.guide.rule.finalised',
    'analytics.throughput.guide.rule.self',
  ],
  tipKeys: [
    'analytics.throughput.guide.tip.context',
    'analytics.throughput.guide.tip.lead',
  ],
  permissionKeys: [
    'analytics.throughput.guide.perm.view',
    'analytics.throughput.guide.perm.self',
  ],
  faq: [
    { questionKey: 'analytics.throughput.guide.faq.self.q', answerKey: 'analytics.throughput.guide.faq.self.a' },
    { questionKey: 'analytics.throughput.guide.faq.lead.q', answerKey: 'analytics.throughput.guide.faq.lead.a' },
  ],
};

/**
 * Per-auditor throughput (PerformanceAnalyticsView): finalised checklist responses, evidence uploaded, exceptions
 * raised, and items assigned — across the WHOLE team, complementing the lead-only performance scorecards. The
 * caller's own row is suppressed server-side unless they hold CIA oversight.
 */
@Component({
  selector: 'app-auditor-throughput',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    RouterLink,
    MatCardModule,
    MatTableModule,
    MatButtonModule,
    MatIconModule,
    TranslatePipe,
    LoadingComponent,
    EmptyStateComponent,
    ErrorStateComponent,
    PageHeaderComponent,
    PageGuideComponent,
  ],
  templateUrl: './auditor-throughput.component.html',
  styleUrl: './auditor-throughput.component.scss',
})
export class AuditorThroughputComponent {
  private readonly service = inject(AnalyticsService);
  /** Resolves auditor user ids to display names in the table. */
  readonly userLookup = inject(UserLookupService);

  readonly guide = THROUGHPUT_GUIDE;

  readonly displayedColumns = ['auditor', 'responded', 'evidence', 'raised', 'assigned'];

  readonly state = signal<ViewState>('loading');
  readonly rows = signal<AuditorThroughputRow[]>([]);

  readonly isEmpty = computed(() => this.state() === 'ready' && this.rows().length === 0);

  constructor() {
    this.fetch();
  }

  fetch(): void {
    this.state.set('loading');
    this.service.auditorThroughput().subscribe({
      next: (rows) => {
        this.rows.set(rows);
        this.state.set('ready');
      },
      error: () => this.state.set('error'),
    });
  }
}
