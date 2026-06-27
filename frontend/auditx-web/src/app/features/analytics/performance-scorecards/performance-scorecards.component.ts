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

import { AnalyticsService } from '../../../core/services/analytics.service';
import { PerformanceScorecard } from '../../../core/models';
import { days } from '../format';
import { LoadingComponent } from '../../../shared/components/loading/loading.component';
import { EmptyStateComponent } from '../../../shared/components/empty-state/empty-state.component';
import { ErrorStateComponent } from '../../../shared/components/error-state/error-state.component';
import { PageHeaderComponent } from '../../../shared/components/page-header/page-header.component';

type ViewState = 'loading' | 'ready' | 'error';

/** Per-audit-lead performance scorecards (PerformanceAnalyticsView). */
@Component({
  selector: 'app-performance-scorecards',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    RouterLink,
    MatCardModule,
    MatTableModule,
    MatButtonModule,
    MatIconModule,
    LoadingComponent,
    EmptyStateComponent,
    ErrorStateComponent,
    PageHeaderComponent,
  ],
  templateUrl: './performance-scorecards.component.html',
  styleUrl: './performance-scorecards.component.scss',
})
export class PerformanceScorecardsComponent {
  private readonly service = inject(AnalyticsService);

  readonly displayedColumns = [
    'lead',
    'auditsLed',
    'auditsCompleted',
    'cycleDays',
    'exceptionsRaised',
    'exceptionsClosed',
    'closureDays',
  ];

  readonly state = signal<ViewState>('loading');
  readonly scorecards = signal<PerformanceScorecard[]>([]);

  readonly days = days;

  readonly isEmpty = computed(
    () => this.state() === 'ready' && this.scorecards().length === 0,
  );

  constructor() {
    this.fetch();
  }

  fetch(): void {
    this.state.set('loading');
    this.service.performanceScorecards().subscribe({
      next: (rows) => {
        this.scorecards.set(rows);
        this.state.set('ready');
      },
      error: () => this.state.set('error'),
    });
  }
}
