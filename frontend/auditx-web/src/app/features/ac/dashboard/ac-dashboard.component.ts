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
import { PageGuideComponent } from '../../../shared/components/page-guide/page-guide.component';
import { PageGuide } from '../../../core/models/page-guide.models';
import { AcAnalyticsSectionsComponent } from '../components/analytics-sections/analytics-sections.component';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';

type ViewState = 'loading' | 'ready' | 'error';

/** Contextual page guide for the AC dashboard (walkthrough + the Overview panel). */
const AC_DASHBOARD_GUIDE: PageGuide = {
  id: 'ac-dashboard',
  titleKey: 'ac.dashboard.title',
  purposeKey: 'ac.dashboard.guide.purpose',
  descriptionKey: 'ac.dashboard.guide.description',
  actionKeys: [
    'ac.dashboard.guide.action.review',
    'ac.dashboard.guide.action.exceptions',
    'ac.dashboard.guide.action.sanctions',
  ],
  sections: [
    {
      selector: 'app-ac-analytics-sections',
      titleKey: 'ac.dashboard.guide.section.metrics.title',
      bodyKey: 'ac.dashboard.guide.section.metrics.body',
    },
  ],
  dependsOnKeys: [
    'ac.dashboard.guide.dep.plans',
    'ac.dashboard.guide.dep.exceptions',
  ],
  tipKeys: ['ac.dashboard.guide.tip.readOnly'],
  permissionKeys: ['ac.dashboard.guide.perm.acMember'],
  faq: [
    {
      questionKey: 'ac.dashboard.guide.faq.identity.q',
      answerKey: 'ac.dashboard.guide.faq.identity.a',
    },
  ],
};

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
    PageGuideComponent,
    AcAnalyticsSectionsComponent,
    TranslatePipe,
  ],
  templateUrl: './ac-dashboard.component.html',
  styleUrl: './ac-dashboard.component.scss',
})
export class AcDashboardComponent {
  private readonly service = inject(AcService);

  readonly guide = AC_DASHBOARD_GUIDE;
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