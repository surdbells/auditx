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
import { PaginatorComponent } from '../../../shared/components/paginator/paginator.component';

type ViewState = 'loading' | 'ready' | 'error';

const DEFAULT_PAGE_SIZE = 25;

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
    MatIconModule,
    TranslatePipe,
    LoadingComponent,
    EmptyStateComponent,
    ErrorStateComponent,
    PageHeaderComponent,
    PaginatorComponent,
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
