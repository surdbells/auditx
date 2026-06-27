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
import { SanctionsCaseListItem } from '../../../../core/models';
import { humanise } from '../humanise';
import { LoadingComponent } from '../../../../shared/components/loading/loading.component';
import { EmptyStateComponent } from '../../../../shared/components/empty-state/empty-state.component';
import { ErrorStateComponent } from '../../../../shared/components/error-state/error-state.component';
import { PageHeaderComponent } from '../../../../shared/components/page-header/page-header.component';

type ViewState = 'loading' | 'ready' | 'error';

const PAGE_SIZE = 20;
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
    LoadingComponent,
    EmptyStateComponent,
    ErrorStateComponent,
    PageHeaderComponent,
  ],
  templateUrl: './dc-queue.component.html',
  styleUrl: './dc-queue.component.scss',
})
export class DcQueueComponent {
  private readonly service = inject(SanctionsService);
  private readonly router = inject(Router);

  readonly displayedColumns = [
    'subject',
    'severity',
    'category',
    'recurrence',
    'triggeredAt',
  ];

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
  }

  subjectLabel(row: SanctionsCaseListItem): string {
    return row.subjectMasked || !row.subjectUserId
      ? MASKED_SUBJECT
      : row.subjectUserId;
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
    this.service.dcQueue(cursor, PAGE_SIZE).subscribe({
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
