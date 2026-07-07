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
import { MatIconModule } from '@angular/material/icon';
import { MatTableModule } from '@angular/material/table';
import { RouterLink } from '@angular/router';

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

type ViewState = 'loading' | 'ready' | 'error';

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
    MatIconModule,
    LoadingComponent,
    EmptyStateComponent,
    ErrorStateComponent,
    PageHeaderComponent,
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
