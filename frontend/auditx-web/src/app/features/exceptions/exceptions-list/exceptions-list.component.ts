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
import { ActivatedRoute, Router } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { provideNativeDateAdapter } from '@angular/material/core';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import { debounceTime } from 'rxjs';

import { ExceptionsService } from '../../../core/services/exceptions.service';
import { UsersService } from '../../../core/services/users.service';
import { AnnualPlansService } from '../../../core/services/annual-plans.service';
import { AuditsService } from '../../../core/services/audits.service';
import {
  AuditListItem,
  ExceptionListItem,
  ExceptionQuery,
  ExceptionSeverity,
  ExceptionStatusFilter,
  PlanListItem,
  UserDto,
} from '../../../core/models';
import { LoadingComponent } from '../../../shared/components/loading/loading.component';
import { EmptyStateComponent } from '../../../shared/components/empty-state/empty-state.component';
import { ErrorStateComponent } from '../../../shared/components/error-state/error-state.component';
import { PageHeaderComponent } from '../../../shared/components/page-header/page-header.component';
import { PaginatorComponent } from '../../../shared/components/paginator/paginator.component';
import { PageGuideComponent } from '../../../shared/components/page-guide/page-guide.component';
import { PageGuide } from '../../../core/models/page-guide.models';
import {
  SearchableSelectComponent,
  SelectOption,
} from '../../../shared/components/searchable-select/searchable-select.component';
import { SavedViewsBarComponent } from '../../../shared/components/saved-views/saved-views-bar.component';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';
import { TranslationService } from '../../../core/i18n/translation.service';

type ViewState = 'loading' | 'ready' | 'error';

const DEFAULT_PAGE_SIZE = 25;

/** Contextual page guide for the exceptions register (drives the walkthrough + the About panel). */
const EXCEPTIONS_GUIDE: PageGuide = {
  id: 'exceptions-list',
  titleKey: 'exceptions.list.title',
  purposeKey: 'exceptions.guide.purpose',
  descriptionKey: 'exceptions.guide.description',
  actionKeys: [
    'exceptions.guide.action.filter',
    'exceptions.guide.action.open',
    'exceptions.guide.action.track',
    'exceptions.guide.action.export',
  ],
  sections: [
    { selector: '.exceptions__filters-card', titleKey: 'exceptions.guide.section.filters.title', bodyKey: 'exceptions.guide.section.filters.body' },
    { selector: '[data-guide="export"]', titleKey: 'exceptions.guide.section.export.title', bodyKey: 'exceptions.guide.section.export.body' },
    { selector: '.exceptions__table', titleKey: 'exceptions.guide.section.table.title', bodyKey: 'exceptions.guide.section.table.body' },
  ],
  workflowKeys: ['exceptions.guide.flow.fieldwork', 'exceptions.guide.flow.raise', 'exceptions.guide.flow.map', 'exceptions.guide.flow.remediate', 'exceptions.guide.flow.close'],
  dependsOnKeys: ['exceptions.guide.dep.audits', 'exceptions.guide.dep.plans', 'exceptions.guide.dep.users'],
  usedByKeys: ['exceptions.guide.use.reports', 'exceptions.guide.use.analytics', 'exceptions.guide.use.sanctions'],
  businessRuleKeys: ['exceptions.guide.rule.lifecycle', 'exceptions.guide.rule.map', 'exceptions.guide.rule.overdue', 'exceptions.guide.rule.recurrence'],
  tipKeys: ['exceptions.guide.tip.filter', 'exceptions.guide.tip.overdue', 'exceptions.guide.tip.export'],
  permissionKeys: ['exceptions.guide.perm.auditor', 'exceptions.guide.perm.owner', 'exceptions.guide.perm.manager'],
  faq: [
    { questionKey: 'exceptions.guide.faq.map.q', answerKey: 'exceptions.guide.faq.map.a' },
    { questionKey: 'exceptions.guide.faq.overdue.q', answerKey: 'exceptions.guide.faq.overdue.a' },
  ],
};

/** Converts a Date to the start-of-day ISO datetime string. */
function toIsoStart(value: Date | null): string | undefined {
  if (!value) {
    return undefined;
  }
  const d = new Date(value);
  d.setHours(0, 0, 0, 0);
  return d.toISOString();
}

/** Converts a Date to the end-of-day ISO datetime string. */
function toIsoEnd(value: Date | null): string | undefined {
  if (!value) {
    return undefined;
  }
  const d = new Date(value);
  d.setHours(23, 59, 59, 999);
  return d.toISOString();
}

@Component({
  selector: 'app-exceptions-list',
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [provideNativeDateAdapter()],
  imports: [
    DatePipe,
    ReactiveFormsModule,
    MatCardModule,
    MatTableModule,
    MatFormFieldModule,
    MatSelectModule,
    MatInputModule,
    MatDatepickerModule,
    MatButtonModule,
    MatIconModule,
    MatTooltipModule,
    SearchableSelectComponent,
    SavedViewsBarComponent,
    LoadingComponent,
    EmptyStateComponent,
    ErrorStateComponent,
    PageHeaderComponent,
    PaginatorComponent,
    PageGuideComponent,
    TranslatePipe,
  ],
  templateUrl: './exceptions-list.component.html',
  styleUrl: './exceptions-list.component.scss',
})
export class ExceptionsListComponent {
  private readonly service = inject(ExceptionsService);
  private readonly users = inject(UsersService);
  private readonly plans = inject(AnnualPlansService);
  private readonly audits = inject(AuditsService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly fb = inject(FormBuilder);
  private readonly i18n = inject(TranslationService);

  readonly guide = EXCEPTIONS_GUIDE;

  readonly displayedColumns = [
    'title',
    'audit',
    'severity',
    'status',
    'owner',
    'raised',
    'targetDate',
    'flags',
  ];

  readonly statuses: { value: ExceptionStatusFilter | 'all'; label: string }[] = [
    { value: 'all', label: this.i18n.translate('exceptions.filter.allStatuses') },
    { value: 'open_any', label: this.i18n.translate('exceptions.status.openAny') },
    { value: 'open', label: this.i18n.translate('exceptions.status.open') },
    { value: 'map_submitted', label: this.i18n.translate('exceptions.status.mapSubmitted') },
    { value: 'map_approved', label: this.i18n.translate('exceptions.status.mapApproved') },
    { value: 'map_rejected', label: this.i18n.translate('exceptions.status.mapRejected') },
    { value: 'pending_closure', label: this.i18n.translate('exceptions.status.pendingClosure') },
    { value: 'closed', label: this.i18n.translate('exceptions.status.closed') },
    { value: 'cancelled', label: this.i18n.translate('exceptions.status.cancelled') },
  ];

  readonly severities: { value: ExceptionSeverity | 'all'; label: string }[] = [
    { value: 'all', label: this.i18n.translate('exceptions.filter.allSeverities') },
    { value: 'low', label: this.i18n.translate('exceptions.severity.low') },
    { value: 'medium', label: this.i18n.translate('exceptions.severity.medium') },
    { value: 'high', label: this.i18n.translate('exceptions.severity.high') },
    { value: 'critical', label: this.i18n.translate('exceptions.severity.critical') },
  ];

  readonly boolFilters: { value: 'all' | 'yes' | 'no'; label: string }[] = [
    { value: 'all', label: this.i18n.translate('exceptions.filter.all') },
    { value: 'yes', label: this.i18n.translate('exceptions.filter.yes') },
    { value: 'no', label: this.i18n.translate('exceptions.filter.no') },
  ];

  readonly filters = this.fb.nonNullable.group({
    search: '',
    status: 'all' as ExceptionStatusFilter | 'all',
    severity: 'all' as ExceptionSeverity | 'all',
    plan: '',
    audit: '',
    overdue: 'all' as 'all' | 'yes' | 'no',
    recurrence: 'all' as 'all' | 'yes' | 'no',
    raisedFrom: null as Date | null,
    raisedTo: null as Date | null,
  });

  readonly state = signal<ViewState>('loading');
  readonly exceptions = signal<ExceptionListItem[]>([]);
  readonly total = signal(0);
  readonly page = signal(1);
  readonly pageSize = signal(DEFAULT_PAGE_SIZE);
  /** In-flight fetch (page navigation / filter change) — disables the paginator without clearing the table. */
  readonly loading = signal(false);
  readonly exporting = signal(false);
  readonly userNames = signal<Record<string, string>>({});
  private readonly planList = signal<PlanListItem[]>([]);
  private readonly auditList = signal<AuditListItem[]>([]);

  /** Plan dropdown: an "all" sentinel plus every annual plan, labelled by period. */
  readonly planOptions = computed<SelectOption[]>(() => [
    { value: '', label: this.i18n.translate('exceptions.filter.allPlans') },
    ...this.planList().map((p) => ({ value: p.id, label: p.periodLabel })),
  ]);

  /** Audit dropdown: an "all" sentinel plus every audit, labelled by name. */
  readonly auditOptions = computed<SelectOption[]>(() => [
    { value: '', label: this.i18n.translate('exceptions.filter.allAudits') },
    ...this.auditList().map((a) => ({ value: a.id, label: a.name })),
  ]);

  private readonly auditNames = computed<Record<string, string>>(() => {
    const map: Record<string, string> = {};
    for (const a of this.auditList()) {
      map[a.id] = a.name;
    }
    return map;
  });

  readonly isEmpty = computed(
    () => this.state() === 'ready' && this.exceptions().length === 0,
  );

  private usersCache: UserDto[] = [];

  /**
   * Extra drilldown filters arriving via query params (dashboard chart clicks) that have no visible
   * filter control on this page. Shown as a dismissible notice; cleared with {@link clearDrilldown}.
   */
  readonly drilldown = signal<{ rootCauseCategory?: string; nonConformanceCategory?: string }>({});

  readonly hasDrilldown = computed(() => {
    const d = this.drilldown();
    return !!(d.rootCauseCategory || d.nonConformanceCategory);
  });

  /** Human-readable summary of the active drilldown filters for the notice chip. */
  readonly drilldownLabel = computed(() => {
    const d = this.drilldown();
    const parts: string[] = [];
    if (d.rootCauseCategory) {
      parts.push(`${this.i18n.translate('exceptions.detail.rootCauseCategory')}: ${d.rootCauseCategory.replaceAll('_', ' ')}`);
    }
    if (d.nonConformanceCategory) {
      parts.push(`${this.i18n.translate('exceptions.detail.nonConformanceCategory')}: ${d.nonConformanceCategory.replaceAll('_', ' ')}`);
    }
    return parts.join(' · ');
  });

  constructor() {
    this.ensureUsers();
    this.loadPlans();
    this.loadAudits();
    this.applyQueryParams();
    this.fetchPage(1);
    this.filters.valueChanges
      .pipe(debounceTime(300), takeUntilDestroyed())
      .subscribe(() => this.fetchPage(1));
  }

  /** Initialise the filters from the URL (dashboard drilldowns) before the first fetch. */
  private applyQueryParams(): void {
    const params = this.route.snapshot.queryParamMap;
    if (params.keys.length === 0) {
      return;
    }

    const patch: Partial<ReturnType<typeof this.filters.getRawValue>> = {};
    const severity = params.get('severity');
    if (severity && this.severities.some((s) => s.value === severity)) {
      patch.severity = severity as ExceptionSeverity;
    }
    const status = params.get('status');
    if (status && this.statuses.some((s) => s.value === status)) {
      patch.status = status as ExceptionStatusFilter;
    }
    const overdue = params.get('overdue');
    if (overdue === 'true' || overdue === 'yes') {
      patch.overdue = 'yes';
    }
    const raisedFrom = params.get('raisedFrom');
    if (raisedFrom && !Number.isNaN(Date.parse(raisedFrom))) {
      patch.raisedFrom = new Date(raisedFrom);
    }
    const raisedTo = params.get('raisedTo');
    if (raisedTo && !Number.isNaN(Date.parse(raisedTo))) {
      patch.raisedTo = new Date(raisedTo);
    }
    if (Object.keys(patch).length > 0) {
      // The debounced valueChanges watcher is not subscribed yet, so this never double-fetches.
      this.filters.patchValue(patch);
    }

    this.drilldown.set({
      rootCauseCategory: params.get('rootCauseCategory') ?? undefined,
      nonConformanceCategory: params.get('nonConformanceCategory') ?? undefined,
    });
  }

  /** Clears the dashboard-drilldown filters (and drops them from the URL) then refetches. */
  clearDrilldown(): void {
    this.drilldown.set({});
    void this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { rootCauseCategory: null, nonConformanceCategory: null },
      queryParamsHandling: 'merge',
      replaceUrl: true,
    });
    this.fetchPage(1);
  }

  private ensureUsers(): void {
    this.users.list({ status: 'active', pageSize: 0 }).subscribe({
      next: (page) => {
        this.usersCache = page.items;
        const map: Record<string, string> = {};
        for (const u of page.items) {
          map[u.id] = u.displayName;
        }
        this.userNames.set(map);
      },
      error: () => {
        // Non-fatal: ids display verbatim.
      },
    });
  }

  /** Eagerly loads the annual-plan directory into the plan dropdown (one capped "load all" call). Non-fatal on error. */
  private loadPlans(): void {
    this.plans.list({ pageSize: 0 }).subscribe({
      next: (result) => this.planList.set(result.items),
      error: () => undefined,
    });
  }

  /** Eagerly loads the audit directory into the audit dropdown + name map (one capped "load all" call). Non-fatal on error. */
  private loadAudits(): void {
    this.audits.list({ pageSize: 0 }).subscribe({
      next: (result) => this.auditList.set(result.items),
      error: () => undefined,
    });
  }

  nameOf(userId: string | null | undefined): string {
    if (!userId) {
      return '—';
    }
    return this.userNames()[userId] ?? userId;
  }

  /** Resolves an audit id to its name for the grid; falls back to the raw id while unresolved. */
  auditNameOf(auditId: string | null | undefined): string {
    if (!auditId) {
      return '—';
    }
    return this.auditNames()[auditId] ?? auditId;
  }

  fetchPage(page: number): void {
    this.loading.set(true);
    this.service
      .list({ ...this.buildFilter(), page, pageSize: this.pageSize() })
      .subscribe({
        next: (result) => {
          this.exceptions.set(result.items);
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

  /** The current filter selection as an ExceptionQuery (no pagination) — shared by the list query and the CSV export. */
  private buildFilter(): ExceptionQuery {
    const { search, status, severity, plan, audit, overdue, recurrence, raisedFrom, raisedTo } =
      this.filters.getRawValue();
    return {
      search: search.trim() || undefined,
      status: status === 'all' ? '' : status,
      severity: severity === 'all' ? '' : severity,
      plan: plan || undefined,
      audit: audit || undefined,
      overdue: overdue === 'all' ? undefined : overdue === 'yes',
      recurrence: recurrence === 'all' ? undefined : recurrence === 'yes',
      raisedFrom: toIsoStart(raisedFrom),
      raisedTo: toIsoEnd(raisedTo),
      // Dashboard-drilldown extras (no visible control here; surfaced via the drilldown notice).
      rootCauseCategory: this.drilldown().rootCauseCategory,
      nonConformanceCategory: this.drilldown().nonConformanceCategory,
    };
  }

  /** Downloads the current filtered finding register as a CSV file. */
  exportCsv(): void {
    if (this.exporting()) {
      return;
    }
    this.exporting.set(true);
    this.service.exportRegister(this.buildFilter()).subscribe({
      next: (response) => {
        this.exporting.set(false);
        const blob = response.body;
        if (!blob) {
          return;
        }
        const url = URL.createObjectURL(blob);
        const anchor = document.createElement('a');
        anchor.href = url;
        anchor.download = `finding-register-${new Date().toISOString().slice(0, 10)}.csv`;
        anchor.click();
        URL.revokeObjectURL(url);
      },
      error: () => this.exporting.set(false),
    });
  }

  /** The current filter selection as a JSON-safe object for a saved view (dates serialised to ISO strings). */
  currentParams(): Record<string, unknown> {
    const v = this.filters.getRawValue();
    return {
      ...v,
      raisedFrom: v.raisedFrom ? v.raisedFrom.toISOString() : null,
      raisedTo: v.raisedTo ? v.raisedTo.toISOString() : null,
    };
  }

  /** Applies a saved view's parameters back onto the filter form (rehydrating dates); the debounced watcher refetches. */
  applyView(params: Record<string, unknown>): void {
    // A saved view is a complete filter state — drop any dashboard-drilldown extras so the applied
    // view shows exactly what it captured (the chip disappears with them).
    if (this.hasDrilldown()) {
      this.drilldown.set({});
      void this.router.navigate([], {
        relativeTo: this.route,
        queryParams: { rootCauseCategory: null, nonConformanceCategory: null },
        queryParamsHandling: 'merge',
        replaceUrl: true,
      });
    }

    const str = (x: unknown, fallback = ''): string => (typeof x === 'string' ? x : fallback);
    const date = (x: unknown): Date | null => (typeof x === 'string' && x ? new Date(x) : null);
    this.filters.patchValue({
      search: str(params['search']),
      status: str(params['status'], 'all') as ExceptionStatusFilter | 'all',
      severity: str(params['severity'], 'all') as ExceptionSeverity | 'all',
      plan: str(params['plan']),
      audit: str(params['audit']),
      overdue: str(params['overdue'], 'all') as 'all' | 'yes' | 'no',
      recurrence: str(params['recurrence'], 'all') as 'all' | 'yes' | 'no',
      raisedFrom: date(params['raisedFrom']),
      raisedTo: date(params['raisedTo']),
    });
  }

  open(row: ExceptionListItem): void {
    void this.router.navigate(['/exceptions', row.id]);
  }
}
