import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  signal,
} from '@angular/core';
import { DatePipe } from '@angular/common';
import { Router } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { IconComponent } from '../../../../core/icons/icon.component';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';

import { SanctionsService } from '../../../../core/services/sanctions.service';
import { UserLookupService } from '../../../../core/services/user-lookup.service';
import { ReferenceDataLookupService } from '../../../../core/services/reference-data-lookup.service';
import { SanctionsCaseListItem } from '../../../../core/models';
import { humanise } from '../humanise';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
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

/** Contextual page guide for the DC queue (drives the walkthrough + the About panel). */
const DC_QUEUE_GUIDE: PageGuide = {
  id: 'sanctions-dc-queue',
  titleKey: 'sanctions.dcQueue.title',
  purposeKey: 'sanctions.dcQueue.guide.purpose',
  descriptionKey: 'sanctions.dcQueue.guide.description',
  actionKeys: [
    'sanctions.dcQueue.guide.action.review',
    'sanctions.dcQueue.guide.action.open',
    'sanctions.dcQueue.guide.action.decide',
    'sanctions.dcQueue.guide.action.page',
  ],
  sections: [
    { selector: '.dc-queue__table', titleKey: 'sanctions.dcQueue.guide.section.queue.title', bodyKey: 'sanctions.dcQueue.guide.section.queue.body' },
    { selector: '.dc-queue__masked', titleKey: 'sanctions.dcQueue.guide.section.subject.title', bodyKey: 'sanctions.dcQueue.guide.section.subject.body' },
    { selector: '.severity-badge', titleKey: 'sanctions.dcQueue.guide.section.severity.title', bodyKey: 'sanctions.dcQueue.guide.section.severity.body' },
  ],
  workflowKeys: [
    'sanctions.dcQueue.guide.flow.trigger',
    'sanctions.dcQueue.guide.flow.recommend',
    'sanctions.dcQueue.guide.flow.refer',
    'sanctions.dcQueue.guide.flow.decide',
    'sanctions.dcQueue.guide.flow.outcome',
  ],
  dependsOnKeys: [
    'sanctions.dcQueue.guide.dep.cases',
    'sanctions.dcQueue.guide.dep.grid',
    'sanctions.dcQueue.guide.dep.identity',
  ],
  usedByKeys: [
    'sanctions.dcQueue.guide.use.case',
    'sanctions.dcQueue.guide.use.hr',
    'sanctions.dcQueue.guide.use.analytics',
  ],
  businessRuleKeys: [
    'sanctions.dcQueue.guide.rule.referred',
    'sanctions.dcQueue.guide.rule.masked',
    'sanctions.dcQueue.guide.rule.recurrence',
    'sanctions.dcQueue.guide.rule.member',
  ],
  tipKeys: [
    'sanctions.dcQueue.guide.tip.severity',
    'sanctions.dcQueue.guide.tip.recurrence',
  ],
  permissionKeys: [
    'sanctions.dcQueue.guide.perm.member',
    'sanctions.dcQueue.guide.perm.view',
  ],
  faq: [
    { questionKey: 'sanctions.dcQueue.guide.faq.here.q', answerKey: 'sanctions.dcQueue.guide.faq.here.a' },
    { questionKey: 'sanctions.dcQueue.guide.faq.masked.q', answerKey: 'sanctions.dcQueue.guide.faq.masked.a' },
  ],
};

@Component({
  selector: 'app-dc-queue',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    DatePipe,
    MatCardModule,
    MatTableModule,
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
  templateUrl: './dc-queue.component.html',
  styleUrl: './dc-queue.component.scss',
})
export class DcQueueComponent {
  private readonly service = inject(SanctionsService);
  private readonly router = inject(Router);
  private readonly userLookup = inject(UserLookupService);
  private readonly refLookup = inject(ReferenceDataLookupService);

  readonly displayedColumns = [
    'subject',
    'severity',
    'category',
    'recurrence',
    'triggeredAt',
  ];

  readonly state = signal<ViewState>('loading');
  readonly cases = signal<SanctionsCaseListItem[]>([]);
  readonly total = signal(0);
  readonly page = signal(1);
  readonly pageSize = signal(DEFAULT_PAGE_SIZE);
  /** In-flight fetch (page navigation) — disables the paginator without clearing the table. */
  readonly loading = signal(false);

  readonly isEmpty = computed(
    () => this.state() === 'ready' && this.cases().length === 0,
  );

  readonly guide = DC_QUEUE_GUIDE;

  readonly humanise = humanise;
  readonly maskedSubject = MASKED_SUBJECT;

  constructor() {
    this.fetchPage(1);
  }

  subjectLabel(row: SanctionsCaseListItem): string {
    return row.subjectMasked || !row.subjectUserId
      ? MASKED_SUBJECT
      : this.userLookup.displayName(row.subjectUserId);
  }

  categoryLabel(code: string | null | undefined): string {
    return code ? this.refLookup.label('sanction_category', code) : '—';
  }

  fetchPage(page: number): void {
    this.loading.set(true);
    this.service.dcQueue(page, this.pageSize()).subscribe({
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
