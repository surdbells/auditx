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
import { MatTooltipModule } from '@angular/material/tooltip';
import { RouterLink } from '@angular/router';
import { forkJoin } from 'rxjs';

import { TranslatePipe } from '../../../core/i18n/translate.pipe';
import { AnalyticsService } from '../../../core/services/analytics.service';
import { RiskHeatmap, RiskRegisterSummary } from '../../../core/models';
import { BAND_COLOR, bandOf } from '../../risks/risk-format';
import { LoadingComponent } from '../../../shared/components/loading/loading.component';
import { EmptyStateComponent } from '../../../shared/components/empty-state/empty-state.component';
import { ErrorStateComponent } from '../../../shared/components/error-state/error-state.component';
import { PageHeaderComponent } from '../../../shared/components/page-header/page-header.component';

type ViewState = 'loading' | 'ready' | 'error';

/** Enterprise risk heatmap + register summary (P1-A, ViewAnalytics). */
@Component({
  selector: 'app-risk-heatmap',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    RouterLink,
    MatCardModule,
    MatButtonModule,
    MatIconModule,
    MatTooltipModule,
    TranslatePipe,
    LoadingComponent,
    EmptyStateComponent,
    ErrorStateComponent,
    PageHeaderComponent,
  ],
  templateUrl: './risk-heatmap.component.html',
  styleUrl: './risk-heatmap.component.scss',
})
export class RiskHeatmapComponent {
  private readonly service = inject(AnalyticsService);

  /** Rows render impact 5→1 (high at top); columns render likelihood 1→5. */
  readonly impacts = [5, 4, 3, 2, 1];
  readonly likelihoods = [1, 2, 3, 4, 5];

  readonly state = signal<ViewState>('loading');
  readonly heatmap = signal<RiskHeatmap | null>(null);
  readonly summary = signal<RiskRegisterSummary | null>(null);

  /** "likelihood-impact" → count, for O(1) cell lookup. */
  private readonly countByCell = computed(() => {
    const map = new Map<string, number>();
    for (const c of this.heatmap()?.cells ?? []) {
      map.set(`${c.likelihood}-${c.impact}`, c.count);
    }
    return map;
  });

  readonly isEmpty = computed(() => this.state() === 'ready' && (this.heatmap()?.totalOpen ?? 0) === 0);

  constructor() {
    this.fetch();
  }

  fetch(): void {
    this.state.set('loading');
    forkJoin({ heatmap: this.service.riskHeatmap(), summary: this.service.riskSummary() }).subscribe({
      next: ({ heatmap, summary }) => {
        this.heatmap.set(heatmap);
        this.summary.set(summary);
        this.state.set('ready');
      },
      error: () => this.state.set('error'),
    });
  }

  count(likelihood: number, impact: number): number {
    return this.countByCell().get(`${likelihood}-${impact}`) ?? 0;
  }

  /** Cell background: band colour at full strength when populated, a faint tint when empty. */
  cellStyle(likelihood: number, impact: number): Record<string, string> {
    const colour = BAND_COLOR[bandOf(likelihood * impact)];
    const populated = this.count(likelihood, impact) > 0;
    return {
      background: `color-mix(in srgb, ${colour} ${populated ? '82%' : '12%'}, transparent)`,
      color: populated ? '#fff' : 'var(--mat-sys-on-surface-variant)',
    };
  }
}
