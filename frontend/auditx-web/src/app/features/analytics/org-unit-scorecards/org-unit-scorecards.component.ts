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
import { OrgUnitScorecard } from '../../../core/models';
import { days } from '../format';
import { LoadingComponent } from '../../../shared/components/loading/loading.component';
import { EmptyStateComponent } from '../../../shared/components/empty-state/empty-state.component';
import { ErrorStateComponent } from '../../../shared/components/error-state/error-state.component';
import { PageHeaderComponent } from '../../../shared/components/page-header/page-header.component';

type ViewState = 'loading' | 'ready' | 'error';

/**
 * Department / business-unit scorecards (ViewAnalytics). Each row aggregates an
 * org unit plus all of its descendants; rows arrive pre-order with a `depth` so
 * the tree can be rendered with indentation. The summary strip sums the root
 * rows only (each root already rolls up its whole subtree) to avoid double
 * counting nested units.
 */
@Component({
  selector: 'app-org-unit-scorecards',
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
  ],
  templateUrl: './org-unit-scorecards.component.html',
  styleUrl: './org-unit-scorecards.component.scss',
})
export class OrgUnitScorecardsComponent {
  private readonly service = inject(AnalyticsService);

  readonly displayedColumns = [
    'unit',
    'entities',
    'auditsCompleted',
    'auditsInFlight',
    'openFindings',
    'criticalOpen',
    'highOpen',
    'closedFindings',
    'closureDays',
  ];

  readonly state = signal<ViewState>('loading');
  readonly scorecards = signal<OrgUnitScorecard[]>([]);

  readonly days = days;

  readonly isEmpty = computed(
    () => this.state() === 'ready' && this.scorecards().length === 0,
  );

  /** Org-wide totals, summed over root units only (each root rolls up its subtree). */
  readonly totals = computed(() => {
    const roots = this.scorecards().filter((s) => s.depth === 0);
    return {
      units: this.scorecards().length,
      entities: roots.reduce((n, s) => n + s.entities, 0),
      openFindings: roots.reduce((n, s) => n + s.openFindings, 0),
      criticalOpen: roots.reduce((n, s) => n + s.criticalOpenFindings, 0),
    };
  });

  constructor() {
    this.fetch();
  }

  fetch(): void {
    this.state.set('loading');
    this.service.orgUnitScorecards().subscribe({
      next: (rows) => {
        this.scorecards.set(rows);
        this.state.set('ready');
      },
      error: () => this.state.set('error'),
    });
  }

  /** Left indent (rem) for a unit's name cell, driven by its tree depth. */
  indent(depth: number): string {
    return `${depth * 1.25}rem`;
  }
}
