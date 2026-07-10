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
import { MatIconModule } from '@angular/material/icon';
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

type ViewState = 'loading' | 'ready' | 'error';

const DEFAULT_PAGE_SIZE = 25;
const MASKED_SUBJECT = 'EMPLOYEE_REDACTED';

@Component({
  selector: 'app-dc-queue',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    DatePipe,
    MatCardModule,
    MatTableModule,
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
