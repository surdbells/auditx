import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  input,
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
import { AuthService } from '../../../core/services/auth.service';
import { Permissions } from '../../../core/permissions';
import { RecurrenceClusterDetail } from '../../../core/models';
import { humanise } from '../format';
import { LoadingComponent } from '../../../shared/components/loading/loading.component';
import { EmptyStateComponent } from '../../../shared/components/empty-state/empty-state.component';
import { ErrorStateComponent } from '../../../shared/components/error-state/error-state.component';
import { PageHeaderComponent } from '../../../shared/components/page-header/page-header.component';
import { PageGuideComponent } from '../../../shared/components/page-guide/page-guide.component';
import { PageGuide } from '../../../core/models/page-guide.models';

type ViewState = 'loading' | 'ready' | 'error';

/** Contextual page guide for a recurrence-cluster drilldown (drives the walkthrough + the About panel). */
const RECURRENCE_CLUSTER_DETAIL_GUIDE: PageGuide = {
  id: 'recurrence-cluster-detail',
  titleKey: 'analytics.detail.title',
  purposeKey: 'analytics.detail.guide.purpose',
  descriptionKey: 'analytics.detail.guide.description',
  actionKeys: ['analytics.detail.guide.action.open'],
  sections: [
    { selector: '.cluster__summary', titleKey: 'analytics.detail.guide.section.summary.title', bodyKey: 'analytics.detail.guide.section.summary.body' },
    { selector: '.cluster__table-card', titleKey: 'analytics.detail.guide.section.members.title', bodyKey: 'analytics.detail.guide.section.members.body' },
  ],
  dependsOnKeys: ['analytics.detail.guide.dep.clusters'],
  usedByKeys: ['analytics.detail.guide.use.exceptions'],
  businessRuleKeys: ['analytics.detail.guide.rule.window'],
  permissionKeys: ['analytics.detail.guide.perm.view'],
};

/** Drilldown for a recurrence cluster: header metadata + member exceptions. */
@Component({
  selector: 'app-recurrence-cluster-detail',
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
    PageGuideComponent,
  ],
  templateUrl: './recurrence-cluster-detail.component.html',
  styleUrl: './recurrence-cluster-detail.component.scss',
})
export class RecurrenceClusterDetailComponent {
  /** Route param bound via withComponentInputBinding. */
  readonly id = input.required<string>();

  private readonly service = inject(AnalyticsService);
  /** Resolves the auditable-entity id to a name in the header. */
  readonly entityLookup = inject(EntityLookupService);
  private readonly auth = inject(AuthService);

  readonly displayedColumns = [
    'title',
    'severity',
    'status',
    'raisedAt',
    'closedAt',
    'actions',
  ];

  readonly state = signal<ViewState>('loading');
  readonly cluster = signal<RecurrenceClusterDetail | null>(null);

  readonly humanise = humanise;

  /** Members link to the M6 exception detail only when the caller may view it. */
  readonly canViewExceptions = computed(() =>
    this.auth.hasPermission(Permissions.ViewExceptions),
  );

  readonly isEmpty = computed(
    () =>
      this.state() === 'ready' && (this.cluster()?.members.length ?? 0) === 0,
  );

  readonly guide = RECURRENCE_CLUSTER_DETAIL_GUIDE;

  constructor() {
    queueMicrotask(() => this.fetch());
  }

  fetch(): void {
    this.state.set('loading');
    this.service.recurrenceClusterDetail(this.id()).subscribe({
      next: (detail) => {
        this.cluster.set(detail);
        this.state.set('ready');
      },
      error: () => this.state.set('error'),
    });
  }
}
