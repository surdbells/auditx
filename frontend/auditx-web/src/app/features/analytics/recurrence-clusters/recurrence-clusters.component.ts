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
import { MatIconModule } from '@angular/material/icon';
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

type ViewState = 'loading' | 'ready' | 'error';

/** Cursor-paged table of detected recurrence clusters; rows drill into detail. */
@Component({
  selector: 'app-recurrence-clusters',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    DatePipe,
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
  readonly nextCursor = signal<string | null>(null);
  readonly hasMore = signal(false);
  readonly loadingMore = signal(false);

  readonly humanise = humanise;

  readonly isEmpty = computed(
    () => this.state() === 'ready' && this.clusters().length === 0,
  );

  constructor() {
    this.fetch();
  }

  fetch(): void {
    this.state.set('loading');
    this.clusters.set([]);
    this.service.recurrenceClusters().subscribe({
      next: (page) => {
        this.clusters.set(page.items);
        this.nextCursor.set(page.nextCursor);
        this.hasMore.set(page.hasMore);
        this.state.set('ready');
      },
      error: () => this.state.set('error'),
    });
  }

  loadMore(): void {
    if (!this.hasMore() || this.loadingMore()) {
      return;
    }
    this.loadingMore.set(true);
    this.service.recurrenceClusters(this.nextCursor()).subscribe({
      next: (page) => {
        this.clusters.update((rows) => [...rows, ...page.items]);
        this.nextCursor.set(page.nextCursor);
        this.hasMore.set(page.hasMore);
        this.loadingMore.set(false);
      },
      error: () => this.loadingMore.set(false),
    });
  }
}
