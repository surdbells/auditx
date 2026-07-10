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
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatTableModule } from '@angular/material/table';
import { debounceTime } from 'rxjs';

import { AuditsService } from '../../../core/services/audits.service';
import { UsersService } from '../../../core/services/users.service';
import { TemplatesService } from '../../../core/services/templates.service';
import { NotificationService } from '../../../core/services/notification.service';
import { AuthService } from '../../../core/services/auth.service';
import { ReferenceDataLookupService } from '../../../core/services/reference-data-lookup.service';
import { Permissions } from '../../../core/permissions';
import {
  AuditListItem,
  AuditStatus,
  CreateAuditRequest,
  TemplateListItem,
  UserDto,
} from '../../../core/models';
import {
  CreateAuditDialogComponent,
  CreateAuditDialogData,
} from '../dialogs/create-audit-dialog.component';
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

/** Contextual page guide for the audits list (drives the walkthrough + the About panel). */
const AUDITS_GUIDE: PageGuide = {
  id: 'audits-list',
  titleKey: 'audits.list.title',
  purposeKey: 'audits.guide.purpose',
  descriptionKey: 'audits.guide.description',
  actionKeys: [
    'audits.guide.action.create',
    'audits.guide.action.filter',
    'audits.guide.action.open',
    'audits.guide.action.track',
  ],
  sections: [
    { selector: '[data-guide="create"]', titleKey: 'audits.guide.section.create.title', bodyKey: 'audits.guide.section.create.body' },
    { selector: '.audits__filters-card', titleKey: 'audits.guide.section.filters.title', bodyKey: 'audits.guide.section.filters.body' },
    { selector: '.audits__table', titleKey: 'audits.guide.section.table.title', bodyKey: 'audits.guide.section.table.body' },
  ],
  workflowKeys: ['audits.guide.flow.plan', 'audits.guide.flow.launch', 'audits.guide.flow.execute', 'audits.guide.flow.findings', 'audits.guide.flow.report'],
  dependsOnKeys: ['audits.guide.dep.plan', 'audits.guide.dep.template', 'audits.guide.dep.universe', 'audits.guide.dep.users'],
  usedByKeys: ['audits.guide.use.findings', 'audits.guide.use.reports', 'audits.guide.use.analytics', 'audits.guide.use.sanctions'],
  businessRuleKeys: ['audits.guide.rule.lifecycle', 'audits.guide.rule.template', 'audits.guide.rule.team', 'audits.guide.rule.cancel'],
  tipKeys: ['audits.guide.tip.filter', 'audits.guide.tip.template', 'audits.guide.tip.reopen'],
  permissionKeys: ['audits.guide.perm.manager', 'audits.guide.perm.auditor', 'audits.guide.perm.admin'],
  faq: [
    { questionKey: 'audits.guide.faq.create.q', answerKey: 'audits.guide.faq.create.a' },
    { questionKey: 'audits.guide.faq.status.q', answerKey: 'audits.guide.faq.status.a' },
  ],
};

@Component({
  selector: 'app-audits-list',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    DatePipe,
    ReactiveFormsModule,
    MatCardModule,
    MatTableModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatButtonModule,
    MatIconModule,
    LoadingComponent,
    EmptyStateComponent,
    ErrorStateComponent,
    PageHeaderComponent,
    PaginatorComponent,
    PageGuideComponent,
    TranslatePipe,
  ],
  templateUrl: './audits-list.component.html',
  styleUrl: './audits-list.component.scss',
})
export class AuditsListComponent {
  private readonly service = inject(AuditsService);
  private readonly users = inject(UsersService);
  private readonly templates = inject(TemplatesService);
  private readonly notify = inject(NotificationService);
  private readonly auth = inject(AuthService);
  private readonly dialog = inject(MatDialog);
  private readonly router = inject(Router);
  private readonly fb = inject(FormBuilder);
  private readonly i18n = inject(TranslationService);
  /** Backs the audit-type filter dropdown and column labels (lazy-loaded). */
  readonly refLookup = inject(ReferenceDataLookupService);

  readonly auditTypes = this.refLookup.options('audit_type');

  readonly guide = AUDITS_GUIDE;

  readonly displayedColumns = [
    'name',
    'auditType',
    'status',
    'window',
    'progress',
  ];

  readonly statuses = computed<{ value: AuditStatus | 'all'; label: string }[]>(
    () => [
      { value: 'all', label: this.i18n.translate('audits.status.all') },
      { value: 'draft', label: this.i18n.translate('audits.status.draft') },
      { value: 'planned', label: this.i18n.translate('audits.status.planned') },
      {
        value: 'in_progress',
        label: this.i18n.translate('audits.status.inProgress'),
      },
      {
        value: 'under_review',
        label: this.i18n.translate('audits.status.underReview'),
      },
      {
        value: 'completed',
        label: this.i18n.translate('audits.status.completed'),
      },
      {
        value: 'cancelled',
        label: this.i18n.translate('audits.status.cancelled'),
      },
    ],
  );

  readonly filters = this.fb.nonNullable.group({
    status: 'all' as AuditStatus | 'all',
    auditType: '',
  });

  readonly state = signal<ViewState>('loading');
  readonly audits = signal<AuditListItem[]>([]);
  readonly total = signal(0);
  readonly page = signal(1);
  readonly pageSize = signal(DEFAULT_PAGE_SIZE);
  /** In-flight fetch (page navigation / filter change) — disables the paginator without clearing the table. */
  readonly loading = signal(false);
  readonly counts = signal<{ status: string; count: number }[]>([]);

  readonly canCreate = computed(() =>
    this.auth.hasPermission(Permissions.CreateAudit),
  );

  readonly isEmpty = computed(
    () => this.state() === 'ready' && this.audits().length === 0,
  );

  private usersCache: UserDto[] = [];
  private templatesCache: TemplateListItem[] = [];

  constructor() {
    this.loadCounts();
    this.fetchPage(1);
    this.filters.valueChanges
      .pipe(debounceTime(300), takeUntilDestroyed())
      .subscribe(() => this.fetchPage(1));
  }

  private loadCounts(): void {
    this.service.counts().subscribe({
      next: (c) =>
        this.counts.set(
          Object.entries(c.byStatus).map(([status, count]) => ({
            status,
            count,
          })),
        ),
      error: () => {
        // Counts are supplementary; leave the summary empty on failure.
      },
    });
  }

  fetchPage(page: number): void {
    this.loading.set(true);
    const { status, auditType } = this.filters.getRawValue();
    this.service
      .list({
        status: status === 'all' ? '' : status,
        auditType: auditType.trim() || undefined,
        page,
        pageSize: this.pageSize(),
      })
      .subscribe({
        next: (result) => {
          this.audits.set(result.items);
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
    const openDialog = (users: UserDto[], templates: TemplateListItem[]): void => {
      const data: CreateAuditDialogData = { users, templates };
      this.dialog
        .open(CreateAuditDialogComponent, { data, width: '640px' })
        .afterClosed()
        .subscribe((result?: CreateAuditRequest) => {
          if (!result) {
            return;
          }
          this.service.create(result).subscribe({
            next: (created) => {
              this.notify.success(
                this.i18n.translate('audits.notify.created', {
                  name: created.name,
                }),
              );
              void this.router.navigate(['/audits', created.id]);
            },
          });
        });
    };

    // Load the published templates (whose checklist can preload) alongside the user list.
    const withTemplates = (users: UserDto[]): void => {
      if (this.templatesCache.length) {
        openDialog(users, this.templatesCache);
        return;
      }
      this.templates.list({ status: 'published', pageSize: 0 }).subscribe({
        next: (page) => {
          this.templatesCache = page.items;
          openDialog(users, page.items);
        },
        error: () => openDialog(users, []),
      });
    };

    if (this.usersCache.length) {
      withTemplates(this.usersCache);
      return;
    }
    this.users.list({ status: 'active', pageSize: 0 }).subscribe({
      next: (page) => {
        this.usersCache = page.items;
        withTemplates(page.items);
      },
      error: () => withTemplates([]),
    });
  }

  open(audit: AuditListItem): void {
    void this.router.navigate(['/audits', audit.id]);
  }
}
