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

type ViewState = 'loading' | 'ready' | 'error';

const PAGE_SIZE = 20;

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
  ],
  templateUrl: './sanctions-tracker.component.html',
  styleUrl: './sanctions-tracker.component.scss',
})
export class SanctionsTrackerComponent {
  private readonly service = inject(SanctionsService);
  /** Resolves unmasked subject user ids to display names. */
  private readonly userLookup = inject(UserLookupService);
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
  readonly nextCursor = signal<string | null>(null);
  readonly hasMore = signal(false);
  readonly loadingMore = signal(false);

  readonly isEmpty = computed(
    () => this.state() === 'ready' && this.cases().length === 0,
  );

  readonly humanise = humanise;
  readonly maskedSubject = MASKED_SUBJECT;

  constructor() {
    this.fetchFirstPage();
    this.filters.valueChanges
      .pipe(takeUntilDestroyed())
      .subscribe(() => this.fetchFirstPage());
  }

  subjectLabel(row: SanctionsCaseListItem): string {
    if (row.subjectMasked || !row.subjectUserId) {
      return MASKED_SUBJECT;
    }
    return this.userLookup.displayName(row.subjectUserId);
  }

  fetchFirstPage(): void {
    this.state.set('loading');
    this.cases.set([]);
    this.nextCursor.set(null);
    this.query(null, (items, cursor, more) => {
      this.cases.set(items);
      this.nextCursor.set(cursor);
      this.hasMore.set(more);
      this.state.set('ready');
    });
  }

  loadMore(): void {
    if (!this.hasMore() || this.loadingMore()) {
      return;
    }
    this.loadingMore.set(true);
    this.query(this.nextCursor(), (items, cursor, more) => {
      this.cases.update((current) => [...current, ...items]);
      this.nextCursor.set(cursor);
      this.hasMore.set(more);
      this.loadingMore.set(false);
    });
  }

  private query(
    cursor: string | null,
    onSuccess: (
      items: SanctionsCaseListItem[],
      cursor: string | null,
      more: boolean,
    ) => void,
  ): void {
    const { status } = this.filters.getRawValue();
    this.service
      .list(status === 'all' ? undefined : status, cursor, PAGE_SIZE)
      .subscribe({
        next: (page) => onSuccess(page.items, page.nextCursor, page.hasMore),
        error: () => {
          if (cursor === null) {
            this.state.set('error');
          } else {
            this.loadingMore.set(false);
          }
        },
      });
  }

  open(row: SanctionsCaseListItem): void {
    void this.router.navigate(['/admin/sanctions', row.id]);
  }
}
