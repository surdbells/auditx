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
import { forkJoin } from 'rxjs';

import { TranslatePipe } from '../../../core/i18n/translate.pipe';
import { TranslationService } from '../../../core/i18n/translation.service';
import { AnalyticsService } from '../../../core/services/analytics.service';
import {
  ComplianceByRegulationRow,
  ControlEffectivenessSummary,
} from '../../../core/models';
import { LoadingComponent } from '../../../shared/components/loading/loading.component';
import { EmptyStateComponent } from '../../../shared/components/empty-state/empty-state.component';
import { ErrorStateComponent } from '../../../shared/components/error-state/error-state.component';
import { PageHeaderComponent } from '../../../shared/components/page-header/page-header.component';

type ViewState = 'loading' | 'ready' | 'error';

/** Controls & Compliance analytics (P1-B, ViewAnalytics): control-effectiveness roll-up + compliance-by-regulation. */
@Component({
  selector: 'app-controls-compliance',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    RouterLink,
    MatCardModule,
    MatButtonModule,
    MatIconModule,
    MatTableModule,
    TranslatePipe,
    LoadingComponent,
    EmptyStateComponent,
    ErrorStateComponent,
    PageHeaderComponent,
  ],
  templateUrl: './controls-compliance.component.html',
  styleUrl: './controls-compliance.component.scss',
})
export class ControlsComplianceComponent {
  private readonly service = inject(AnalyticsService);
  private readonly i18n = inject(TranslationService);

  readonly regulationColumns = ['code', 'name', 'authority', 'linked', 'open'];

  readonly state = signal<ViewState>('loading');
  readonly effectiveness = signal<ControlEffectivenessSummary | null>(null);
  readonly compliance = signal<ComplianceByRegulationRow[]>([]);

  readonly isEmpty = computed(
    () => this.state() === 'ready' && (this.effectiveness()?.totalActive ?? 0) === 0 && this.compliance().length === 0,
  );

  /** Effective %, over tested controls (avoids dividing by not-yet-tested). */
  readonly effectiveShare = computed(() => {
    const summary = this.effectiveness();
    if (!summary) {
      return null;
    }
    const effective = summary.byEffectiveness.find((c) => c.key === 'effective')?.count ?? 0;
    return summary.tested > 0 ? Math.round((effective / summary.tested) * 100) : null;
  });

  constructor() {
    this.fetch();
  }

  fetch(): void {
    this.state.set('loading');
    forkJoin({
      effectiveness: this.service.controlEffectiveness(),
      compliance: this.service.complianceByRegulation(),
    }).subscribe({
      next: ({ effectiveness, compliance }) => {
        this.effectiveness.set(effectiveness);
        this.compliance.set(compliance);
        this.state.set('ready');
      },
      error: () => this.state.set('error'),
    });
  }

  /** Localised label for an effectiveness key (falls back to the raw key). */
  effectivenessLabel(key: string): string {
    return this.i18n.translate('control.effectiveness.' + key);
  }

  /** Localised label for a control-type key. */
  typeLabel(key: string): string {
    return this.i18n.translate('control.type.' + key);
  }
}
