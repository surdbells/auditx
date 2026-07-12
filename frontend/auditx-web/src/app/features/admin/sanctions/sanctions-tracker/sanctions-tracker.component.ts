import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  signal,
} from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Router } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { IconComponent } from '../../../../core/icons/icon.component';
import { MatSelectModule } from '@angular/material/select';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';

import { SanctionsService } from '../../../../core/services/sanctions.service';
import { UserLookupService } from '../../../../core/services/user-lookup.service';
import { ReferenceDataLookupService } from '../../../../core/services/reference-data-lookup.service';
import {
  SanctionsCaseListItem,
  SanctionsCaseStatus,
} from '../../../../core/models';
import { humanise } from '../humanise';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { TranslationService } from '../../../../core/i18n/translation.service';
import { LoadingComponent } from '../../../../shared/components/loading/loading.component';
import { EmptyStateComponent } from '../../../../shared/components/empty-state/empty-state.component';
import { ErrorStateComponent } from '../../../../shared/components/error-state/error-state.component';
import { PageHeaderComponent } from '../../../../shared/components/page-header/page-header.component';
import { PaginatorComponent } from '../../../../shared/components/paginator/paginator.component';
import { PageGuideComponent } from '../../../../shared/components/page-guide/page-guide.component';
import { PageGuide } from '../../../../core/models/page-guide.models';

type ViewState = 'loading' | 'ready' | 'error';

const DEFAULT_PAGE_SIZE = 25;

const MASKED_SUBJECT = 'EMPLOYEE_REDACTED';

/** Contextual page guide for the sanctions tracker (drives the walkthrough + the About panel). */
const SANCTIONS_GUIDE: PageGuide = {
  id: 'sanctions-tracker',
  titleKey: 'sanctions.tracker.title',
  purposeKey: 'sanctions.guide.purpose',
  descriptionKey: 'sanctions.guide.description',
  actionKeys: [
    'sanctions.guide.action.filter',
    'sanctions.guide.action.open',
    'sanctions.guide.action.track',
  ],
  sections: [
    { selector: '.sanctions__filters-card', titleKey: 'sanctions.guide.section.filters.title', bodyKey: 'sanctions.guide.section.filters.body' },
    { selector: '.sanctions__table', titleKey: 'sanctions.guide.section.table.title', bodyKey: 'sanctions.guide.section.table.body' },
    { selector: '.sanctions__masked', titleKey: 'sanctions.guide.section.masked.title', bodyKey: 'sanctions.guide.section.masked.body' },
  ],
  workflowKeys: [
    'sanctions.guide.flow.finding',
    'sanctions.guide.flow.recommendation',
    'sanctions.guide.flow.hr',
    'sanctions.guide.flow.dc',
    'sanctions.guide.flow.appeal',
  ],
  dependsOnKeys: [
    'sanctions.guide.dep.findings',
    'sanctions.guide.dep.audits',
    'sanctions.guide.dep.identity',
  ],
  usedByKeys: [
    'sanctions.guide.use.reports',
    'sanctions.guide.use.analytics',
    'sanctions.guide.use.notifications',
  ],
  businessRuleKeys: [
    'sanctions.guide.rule.masking',
    'sanctions.guide.rule.lifecycle',
    'sanctions.guide.rule.recurrence',
    'sanctions.guide.rule.appeal',
  ],
  tipKeys: [
    'sanctions.guide.tip.status',
    'sanctions.guide.tip.recurrence',
  ],
  permissionKeys: [
    'sanctions.guide.perm.hr',
    'sanctions.guide.perm.admin',
  ],
  faq: [
    { questionKey: 'sanctions.guide.faq.masking.q', answerKey: 'sanctions.guide.faq.masking.a' },
    { questionKey: 'sanctions.guide.faq.origin.q', answerKey: 'sanctions.guide.faq.origin.a' },
  ],
};

@Component({
  selector: 'app-sanctions-tracker',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    DatePipe,
    ReactiveFormsModule,
    MatCardModule,
    MatTableModule,
    MatFormFieldModule,
    MatSelectModule,
    MatButtonModule,
    IconComponent,
    MatTooltipModule,
    TranslatePipe,
    LoadingComponent,
    EmptyStateComponent,
    ErrorStateComponent,
    PageHeaderComponent,
    PaginatorComponent,
    PageGuideComponent,
  ],
  templateUrl: './sanctions-tracker.component.html',
  styleUrl: './sanctions-tracker.component.scss',
})
export class SanctionsTrackerComponent {
  private readonly service = inject(SanctionsService);
  /** Resolves unmasked subject user ids to display names. */
  private readonly userLookup = inject(UserLookupService);
  private readonly refLookup = inject(ReferenceDataLookupService);
  private readonly router = inject(Router);
  private readonly fb = inject(FormBuilder);
  private readonly i18n = inject(TranslationService);

  readonly displayedColumns = [
    'subject',
    'severity',
    'status',
    'category',
    'recurrence',
    'triggeredAt',
  ];

  readonly statuses: { value: SanctionsCaseStatus | 'all'; label: string }[] = [
    { value: 'all', label: this.i18n.translate('sanctions.status.all') },
    {
      value: 'recommendation_drafted',
      label: this.i18n.translate('sanctions.status.recommendation_drafted'),
    },
    {
      value: 'recommendation_submitted',
      label: this.i18n.translate('sanctions.status.recommendation_submitted'),
    },
    {
      value: 'hr_outcome_recorded',
      label: this.i18n.translate('sanctions.status.hr_outcome_recorded'),
    },
    {
      value: 'dc_referral',
      label: this.i18n.translate('sanctions.status.dc_referral'),
    },
    {
      value: 'dc_decision_recorded',
      label: this.i18n.translate('sanctions.status.dc_decision_recorded'),
    },
    {
      value: 'appealed',
      label: this.i18n.translate('sanctions.status.appealed'),
    },
    {
      value: 'appeal_decision_recorded',
      label: this.i18n.translate('sanctions.status.appeal_decision_recorded'),
    },
    {
      value: 'closed',
      label: this.i18n.translate('sanctions.status.closed'),
    },
  ];

  readonly filters = this.fb.nonNullable.group({
    status: 'all' as SanctionsCaseStatus | 'all',
  });

  readonly state = signal<ViewState>('loading');
  readonly cases = signal<SanctionsCaseListItem[]>([]);
  readonly total = signal(0);
  readonly page = signal(1);
  readonly pageSize = signal(DEFAULT_PAGE_SIZE);
  /** In-flight fetch (page navigation / filter change) — disables the paginator without clearing the table. */
  readonly loading = signal(false);

  readonly isEmpty = computed(
    () => this.state() === 'ready' && this.cases().length === 0,
  );

  readonly humanise = humanise;
  readonly maskedSubject = MASKED_SUBJECT;
  readonly guide = SANCTIONS_GUIDE;

  constructor() {
    this.fetchPage(1);
    this.filters.valueChanges
      .pipe(takeUntilDestroyed())
      .subscribe(() => this.fetchPage(1));
  }

  subjectLabel(row: SanctionsCaseListItem): string {
    if (row.subjectMasked || !row.subjectUserId) {
      return MASKED_SUBJECT;
    }
    return this.userLookup.displayName(row.subjectUserId);
  }

  categoryLabel(code: string | null | undefined): string {
    return code ? this.refLookup.label('sanction_category', code) : '—';
  }

  fetchPage(page: number): void {
    this.loading.set(true);
    const { status } = this.filters.getRawValue();
    this.service
      .list(status === 'all' ? undefined : status, page, this.pageSize())
      .subscribe({
        next: (result) => {
          this.cases.set(result.items);
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

  open(row: SanctionsCaseListItem): void {
    void this.router.navigate(['/admin/sanctions', row.id]);
  }
}
