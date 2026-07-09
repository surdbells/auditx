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
import { UserLookupService } from '../../../core/services/user-lookup.service';
import { BudgetVsActualRow, UtilisationRow } from '../../../core/models';
import { humanise, percent } from '../format';
import { LoadingComponent } from '../../../shared/components/loading/loading.component';
import { EmptyStateComponent } from '../../../shared/components/empty-state/empty-state.component';
import { ErrorStateComponent } from '../../../shared/components/error-state/error-state.component';
import { PageHeaderComponent } from '../../../shared/components/page-header/page-header.component';

type ViewState = 'loading' | 'ready' | 'error';

/** Time & Budget analytics (ViewAnalytics): budget-vs-actual per audit + utilisation per auditor. */
@Component({
  selector: 'app-time-budget',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
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
  templateUrl: './time-budget.component.html',
  styleUrl: './time-budget.component.scss',
})
export class TimeBudgetComponent {
  private readonly service = inject(AnalyticsService);
  private readonly i18n = inject(TranslationService);
  /** Resolves audit-lead / auditor user ids to display names. */
  readonly userLookup = inject(UserLookupService);

  readonly budgetColumns = ['audit', 'status', 'lead', 'budget', 'actual', 'variance', 'consumed'];
  readonly utilColumns = ['auditor', 'total', 'audits', 'split'];

  readonly state = signal<ViewState>('loading');
  readonly budget = signal<BudgetVsActualRow[]>([]);
  readonly utilisation = signal<UtilisationRow[]>([]);

  readonly percent = percent;
  readonly humanise = humanise;

  readonly isEmpty = computed(
    () => this.state() === 'ready' && this.budget().length === 0 && this.utilisation().length === 0,
  );

  constructor() {
    this.fetch();
  }

  fetch(): void {
    this.state.set('loading');
    forkJoin({
      budget: this.service.budgetVsActual(),
      utilisation: this.service.utilisation(),
    }).subscribe({
      next: ({ budget, utilisation }) => {
        this.budget.set(budget);
        this.utilisation.set(utilisation);
        this.state.set('ready');
      },
      error: () => this.state.set('error'),
    });
  }

  /** A compact "Fieldwork 6 · Review 2" split for the utilisation row. */
  splitLabel(row: UtilisationRow): string {
    return row.byCategory
      .map((c) => `${this.i18n.translate('time.category.' + c.category)} ${c.hours}`)
      .join(' · ');
  }
}
