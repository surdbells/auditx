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
import { IconComponent } from '../../../core/icons/icon.component';
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
import { PaginatorComponent } from '../../../shared/components/paginator/paginator.component';
import { PageGuideComponent } from '../../../shared/components/page-guide/page-guide.component';
import { PageGuide } from '../../../core/models/page-guide.models';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';
import { TranslationService } from '../../../core/i18n/translation.service';

type ViewState = 'loading' | 'ready' | 'error';

const DEFAULT_PAGE_SIZE = 25;

/** Contextual page guide for the annual plans list (drives the walkthrough + the About panel). */
const PLANS_GUIDE: PageGuide = {
  id: 'plans-list',
  titleKey: 'planning.list.title',
  purposeKey: 'planning.guide.purpose',
  descriptionKey: 'planning.guide.description',
  actionKeys: [
    'planning.guide.action.create',
    'planning.guide.action.filter',
    'planning.guide.action.open',
    'planning.guide.action.track',
  ],
  sections: [
    { selector: '[data-guide="create"]', titleKey: 'planning.guide.section.create.title', bodyKey: 'planning.guide.section.create.body' },
    { selector: '.plans__filters-card', titleKey: 'planning.guide.section.filters.title', bodyKey: 'planning.guide.section.filters.body' },
    { selector: '.plans__table', titleKey: 'planning.guide.section.table.title', bodyKey: 'planning.guide.section.table.body' },
  ],
  workflowKeys: ['planning.guide.flow.draft', 'planning.guide.flow.submit', 'planning.guide.flow.approve', 'planning.guide.flow.audits', 'planning.guide.flow.close'],
  dependsOnKeys: ['planning.guide.dep.universe', 'planning.guide.dep.risk', 'planning.guide.dep.users'],
  usedByKeys: ['planning.guide.use.audits', 'planning.guide.use.reports', 'planning.guide.use.analytics'],
  businessRuleKeys: ['planning.guide.rule.lifecycle', 'planning.guide.rule.approval', 'planning.guide.rule.items', 'planning.guide.rule.locked'],
  tipKeys: ['planning.guide.tip.filter', 'planning.guide.tip.revisions'],
  permissionKeys: ['planning.guide.perm.manage', 'planning.guide.perm.approve'],
  faq: [
    { questionKey: 'planning.guide.faq.create.q', answerKey: 'planning.guide.faq.create.a' },
    { questionKey: 'planning.guide.faq.audits.q', answerKey: 'planning.guide.faq.audits.a' },
  ],
};

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
    IconComponent,
    LoadingComponent,
    EmptyStateComponent,
    ErrorStateComponent,
    PageHeaderComponent,
    PaginatorComponent,
    PageGuideComponent,
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

  readonly guide = PLANS_GUIDE;

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
  readonly total = signal(0);
  readonly page = signal(1);
  readonly pageSize = signal(DEFAULT_PAGE_SIZE);
  /** In-flight fetch (page navigation / filter change) — disables the paginator without clearing the table. */
  readonly loading = signal(false);

  readonly canManage = computed(() =>
    this.auth.hasPermission(Permissions.ManagePlan),
  );

  readonly isEmpty = computed(
    () => this.state() === 'ready' && this.plans().length === 0,
  );

  constructor() {
    this.fetchPage(1);
    this.filters.valueChanges
      .pipe(takeUntilDestroyed())
      .subscribe(() => this.fetchPage(1));
  }

  fetchPage(page: number): void {
    this.loading.set(true);
    const status = this.filters.getRawValue().status;
    this.service
      .list({
        status: status === 'all' ? '' : status,
        page,
        pageSize: this.pageSize(),
      })
      .subscribe({
        next: (result) => {
          this.plans.set(result.items);
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
