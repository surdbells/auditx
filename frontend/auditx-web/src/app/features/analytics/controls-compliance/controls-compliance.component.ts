import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  signal,
} from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { IconComponent } from '../../../core/icons/icon.component';
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
import { PageGuideComponent } from '../../../shared/components/page-guide/page-guide.component';
import { PageGuide } from '../../../core/models/page-guide.models';

type ViewState = 'loading' | 'ready' | 'error';

/** Contextual page guide for controls & compliance analytics (drives the walkthrough + the About panel). */
const CONTROLS_COMPLIANCE_GUIDE: PageGuide = {
  id: 'controls-compliance',
  titleKey: 'analytics.controlsCompliance.title',
  purposeKey: 'analytics.controlsCompliance.guide.purpose',
  descriptionKey: 'analytics.controlsCompliance.guide.description',
  actionKeys: [
    'analytics.controlsCompliance.guide.action.effectiveness',
    'analytics.controlsCompliance.guide.action.breakdown',
    'analytics.controlsCompliance.guide.action.compliance',
    'analytics.controlsCompliance.guide.action.refresh',
  ],
  sections: [
    { selector: '.cc__summary-card', titleKey: 'analytics.controlsCompliance.guide.section.summary.title', bodyKey: 'analytics.controlsCompliance.guide.section.summary.body' },
    { selector: '.cc__breakdowns', titleKey: 'analytics.controlsCompliance.guide.section.breakdowns.title', bodyKey: 'analytics.controlsCompliance.guide.section.breakdowns.body' },
    { selector: '.cc__table-card', titleKey: 'analytics.controlsCompliance.guide.section.compliance.title', bodyKey: 'analytics.controlsCompliance.guide.section.compliance.body' },
  ],
  workflowKeys: [
    'analytics.controlsCompliance.guide.flow.define',
    'analytics.controlsCompliance.guide.flow.test',
    'analytics.controlsCompliance.guide.flow.rate',
    'analytics.controlsCompliance.guide.flow.link',
    'analytics.controlsCompliance.guide.flow.report',
  ],
  dependsOnKeys: [
    'analytics.controlsCompliance.guide.dep.controls',
    'analytics.controlsCompliance.guide.dep.regulations',
    'analytics.controlsCompliance.guide.dep.findings',
  ],
  usedByKeys: [
    'analytics.controlsCompliance.guide.use.reports',
    'analytics.controlsCompliance.guide.use.dashboards',
    'analytics.controlsCompliance.guide.use.audits',
  ],
  businessRuleKeys: [
    'analytics.controlsCompliance.guide.rule.share',
    'analytics.controlsCompliance.guide.rule.active',
    'analytics.controlsCompliance.guide.rule.open',
  ],
  tipKeys: [
    'analytics.controlsCompliance.guide.tip.ineffective',
    'analytics.controlsCompliance.guide.tip.register',
  ],
  permissionKeys: [
    'analytics.controlsCompliance.guide.perm.viewer',
    'analytics.controlsCompliance.guide.perm.manager',
  ],
  faq: [
    { questionKey: 'analytics.controlsCompliance.guide.faq.share.q', answerKey: 'analytics.controlsCompliance.guide.faq.share.a' },
    { questionKey: 'analytics.controlsCompliance.guide.faq.empty.q', answerKey: 'analytics.controlsCompliance.guide.faq.empty.a' },
  ],
};

/** Controls & Compliance analytics (P1-B, ViewAnalytics): control-effectiveness roll-up + compliance-by-regulation. */
@Component({
  selector: 'app-controls-compliance',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    RouterLink,
    MatCardModule,
    MatButtonModule,
    IconComponent,
    MatTableModule,
    TranslatePipe,
    LoadingComponent,
    EmptyStateComponent,
    ErrorStateComponent,
    PageHeaderComponent,
    PageGuideComponent,
  ],
  templateUrl: './controls-compliance.component.html',
  styleUrl: './controls-compliance.component.scss',
})
export class ControlsComplianceComponent {
  private readonly service = inject(AnalyticsService);
  private readonly i18n = inject(TranslationService);

  readonly guide = CONTROLS_COMPLIANCE_GUIDE;

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
