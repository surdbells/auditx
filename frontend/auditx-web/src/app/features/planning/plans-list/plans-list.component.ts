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
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatSelectModule } from '@angular/material/select';
import { MatTableModule } from '@angular/material/table';

import { AnnualPlansService } from '../../../core/services/annual-plans.service';
import { NotificationService } from '../../../core/services/notification.service';
import { AuthService } from '../../../core/services/auth.service';
import { Permissions } from '../../../core/permissions';
import { PlanListItem, PlanStatus, SavePlanRequest } from '../../../core/models';
import {
  PlanDialogComponent,
  PlanDialogData,
} from '../dialogs/plan-dialog.component';
import { LoadingComponent } from '../../../shared/components/loading/loading.component';
import { EmptyStateComponent } from '../../../shared/components/empty-state/empty-state.component';
import { ErrorStateComponent } from '../../../shared/components/error-state/error-state.component';
import { PageHeaderComponent } from '../../../shared/components/page-header/page-header.component';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';
import { TranslationService } from '../../../core/i18n/translation.service';

type ViewState = 'loading' | 'ready' | 'error';

const PAGE_SIZE = 20;

@Component({
  selector: 'app-plans-list',
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
    LoadingComponent,
    EmptyStateComponent,
    ErrorStateComponent,
    PageHeaderComponent,
    TranslatePipe,
  ],
  templateUrl: './plans-list.component.html',
  styleUrl: './plans-list.component.scss',
})
export class PlansListComponent {
  private readonly service = inject(AnnualPlansService);
  private readonly notify = inject(NotificationService);
  private readonly auth = inject(AuthService);
  private readonly dialog = inject(MatDialog);
  private readonly router = inject(Router);
  private readonly fb = inject(FormBuilder);
  private readonly i18n = inject(TranslationService);

  readonly displayedColumns = ['periodLabel', 'period', 'status', 'itemCount'];

  readonly statuses: { value: PlanStatus | 'all'; labelKey: string }[] = [
    { value: 'all', labelKey: 'planning.status.all' },
    { value: 'draft', labelKey: 'planning.status.draft' },
    { value: 'submitted', labelKey: 'planning.status.submitted' },
    { value: 'revisions_requested', labelKey: 'planning.status.revisionsRequested' },
    { value: 'revision_submitted', labelKey: 'planning.status.revisionSubmitted' },
    { value: 'approved', labelKey: 'planning.status.approved' },
    { value: 'closed', labelKey: 'planning.status.closed' },
  ];

  readonly filters = this.fb.nonNullable.group({
    status: 'all' as PlanStatus | 'all',
  });

  readonly state = signal<ViewState>('loading');
  readonly plans = signal<PlanListItem[]>([]);
  readonly nextCursor = signal<string | null>(null);
  readonly hasMore = signal(false);
  readonly loadingMore = signal(false);

  readonly canManage = computed(() =>
    this.auth.hasPermission(Permissions.ManagePlan),
  );

  readonly isEmpty = computed(
    () => this.state() === 'ready' && this.plans().length === 0,
  );

  constructor() {
    this.fetchFirstPage();
    this.filters.valueChanges
      .pipe(takeUntilDestroyed())
      .subscribe(() => this.fetchFirstPage());
  }

  fetchFirstPage(): void {
    this.state.set('loading');
    this.plans.set([]);
    this.nextCursor.set(null);
    this.query(null, (items, cursor, more) => {
      this.plans.set(items);
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
      this.plans.update((current) => [...current, ...items]);
      this.nextCursor.set(cursor);
      this.hasMore.set(more);
      this.loadingMore.set(false);
    });
  }

  private query(
    cursor: string | null,
    onSuccess: (
      items: PlanListItem[],
      cursor: string | null,
      more: boolean,
    ) => void,
  ): void {
    const status = this.filters.getRawValue().status;
    this.service
      .list({
        status: status === 'all' ? '' : status,
        cursor,
        limit: PAGE_SIZE,
      })
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

  create(): void {
    const data: PlanDialogData = {};
    this.dialog
      .open(PlanDialogComponent, { data, width: '480px' })
      .afterClosed()
      .subscribe((result?: SavePlanRequest) => {
        if (!result) {
          return;
        }
        this.service.create(result).subscribe({
          next: (created) => {
            this.notify.success(
              this.i18n.translate('planning.toast.created', {
                label: created.periodLabel,
              }),
            );
            void this.router.navigate(['/planning', created.id]);
          },
        });
      });
  }

  open(plan: PlanListItem): void {
    void this.router.navigate(['/planning', plan.id]);
  }
}
