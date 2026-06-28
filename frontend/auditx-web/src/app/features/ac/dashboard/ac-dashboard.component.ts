import {
  ChangeDetectionStrategy,
  Component,
  inject,
  signal,
} from '@angular/core';

import { AcService } from '../../../core/services/ac.service';
import { AcDashboard } from '../../../core/models';
import { LoadingComponent } from '../../../shared/components/loading/loading.component';
import { ErrorStateComponent } from '../../../shared/components/error-state/error-state.component';
import { PageHeaderComponent } from '../../../shared/components/page-header/page-header.component';
import { AcAnalyticsSectionsComponent } from '../components/analytics-sections/analytics-sections.component';

type ViewState = 'loading' | 'ready' | 'error';

/**
 * Read-only AC dashboard: live aggregates from M9 analytics. No edit controls;
 * sanctions are aggregate-only (no subject identity); restricted material
 * findings are pre-filtered by the backend. Renders only what the API returns.
 */
@Component({
  selector: 'app-ac-dashboard',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    LoadingComponent,
    ErrorStateComponent,
    PageHeaderComponent,
    AcAnalyticsSectionsComponent,
  ],
  templateUrl: './ac-dashboard.component.html',
  styleUrl: './ac-dashboard.component.scss',
})
export class AcDashboardComponent {
  private readonly service = inject(AcService);

  readonly state = signal<ViewState>('loading');
  readonly dashboard = signal<AcDashboard | null>(null);

  constructor() {
    queueMicrotask(() => this.fetch());
  }

  fetch(): void {
    this.state.set('loading');
    this.service.getDashboard().subscribe({
      next: (dashboard) => {
        this.dashboard.set(dashboard);
        this.state.set('ready');
      },
      error: () => this.state.set('error'),
    });
  }
}