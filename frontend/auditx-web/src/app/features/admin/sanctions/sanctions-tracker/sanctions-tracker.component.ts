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
import { MatIconModule } from '@angular/material/icon';
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

type ViewState = 'loading' | 'ready' | 'error';

const DEFAULT_PAGE_SIZE = 25;

const MASKED_SUBJECT = 'EMPLOYEE_REDACTED';

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
    MatIconModule,
    MatTooltipModule,
    TranslatePipe,
    LoadingComponent,
    EmptyStateComponent,
    ErrorStateComponent,
    PageHeaderComponent,
    PaginatorComponent,
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
