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
import { RouterLink } from '@angular/router';

import { TranslatePipe } from '../../../core/i18n/translate.pipe';
import { AnalyticsService } from '../../../core/services/analytics.service';
import { DashboardListItem } from '../../../core/models';
import { LoadingComponent } from '../../../shared/components/loading/loading.component';
import { EmptyStateComponent } from '../../../shared/components/empty-state/empty-state.component';
import { ErrorStateComponent } from '../../../shared/components/error-state/error-state.component';
import { PageHeaderComponent } from '../../../shared/components/page-header/page-header.component';

type ViewState = 'loading' | 'ready' | 'error';

/** Lists the analytics dashboards the caller may see, as navigable cards. */
@Component({
  selector: 'app-dashboards-list',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    RouterLink,
    MatCardModule,
    MatButtonModule,
    MatIconModule,
    TranslatePipe,
    LoadingComponent,
    EmptyStateComponent,
    ErrorStateComponent,
    PageHeaderComponent,
  ],
  templateUrl: './dashboards-list.component.html',
  styleUrl: './dashboards-list.component.scss',
})
export class DashboardsListComponent {
  private readonly service = inject(AnalyticsService);

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
